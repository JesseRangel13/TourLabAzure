using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Azure.Messaging.ServiceBus;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using TourLab.Azure.Contracts;
using TourLab.Azure.Infrastructure.Messaging;
namespace TourLab.Azure.Infrastructure.Operations;
public sealed record DeadLetterView(Guid Id,string Source,long SequenceNumber,Guid? LogicalMessageId,string Type,Guid? SagaId,string Reason,bool Replayable);
public sealed record ReplayView(Guid Id,string Outcome);
public sealed partial class ServiceOperations {
 private ServiceBusTransport Broker {
  get{RequireLab();if(!services.GetRequiredService<IOptions<MessagingOptions>>().Value.Enabled)throw new OperationsProblem(503,"BrokerDisabled");return services.GetRequiredService<ServiceBusTransport>();}
 }
 private ServiceBusReceiver Receiver(string source)=>source switch {
  "commands"=>Broker.Client.CreateReceiver(BusTopology.CommandQueue(Schema),new ServiceBusReceiverOptions{SubQueue=SubQueue.DeadLetter,ReceiveMode=ServiceBusReceiveMode.PeekLock,PrefetchCount=0}),
  "events"=>Broker.Client.CreateReceiver(MessageRoutes.Topic,Schema+"-events",new ServiceBusReceiverOptions{SubQueue=SubQueue.DeadLetter,ReceiveMode=ServiceBusReceiveMode.PeekLock,PrefetchCount=0}),
  _=>throw new OperationsProblem(400,"InvalidDlqSource")
 };
 private IntegrationEnvelope<JsonElement>? ReplayEnvelope(ServiceBusReceivedMessage message,string source) {
  try {
   var envelope=MessageCodec.Parse(message.Body.ToString());
   if(message.Body.ToMemory().Length>256*1024||!BusTopology.Accepts(Schema,source=="events",envelope.Type)||
    envelope.OccurredAtUtc<clock.GetUtcNow().AddDays(-14)||envelope.OccurredAtUtc>clock.GetUtcNow().AddMinutes(5))return null;
   return envelope;
  }catch(PoisonMessageException){return null;}
 }
 public async Task<DeadLetterView[]> InspectAsync(string source,int size,long? from,string actor,CancellationToken ct) {
  if(size is <1 or >10||from<0)throw new OperationsProblem(400,"InvalidPagination");
  using var budget=CancellationTokenSource.CreateLinkedTokenSource(ct);budget.CancelAfter(TimeSpan.FromSeconds(20));
  await using var receiver=Receiver(source);
  var messages=await receiver.PeekMessagesAsync(size,from,budget.Token);
  await using var connection=await Open(budget.Token);var result=new List<DeadLetterView>();
  foreach(var message in messages){
   var hash=Convert.ToHexString(SHA256.HashData(message.Body.ToMemory().Span));
   var id=new Guid(SHA256.HashData(Encoding.UTF8.GetBytes(Schema+"/"+source+"/"+message.SequenceNumber+"/"+hash)).AsSpan(0,16));
   var envelope=ReplayEnvelope(message,source);
   using var save=Command(connection,$"""
    IF NOT EXISTS(SELECT 1 FROM [{Schema}].[DlqReplays] WITH(UPDLOCK,HOLDLOCK) WHERE Id=@id)
      INSERT INTO [{Schema}].[DlqReplays](Id,Source,SequenceNumber,BodyHash,LogicalMessageId,InspectedAtUtc,Status)
      VALUES(@id,@source,@sequence,@hash,@logical,@now,'Inspected');
    ELSE UPDATE [{Schema}].[DlqReplays] SET InspectedAtUtc=@now WHERE Id=@id AND Status<>'Completed' AND (LeaseUntilUtc IS NULL OR LeaseUntilUtc<=@now);
    """,("@id",id),("@source",source),("@sequence",message.SequenceNumber),("@hash",hash),("@logical",envelope?.MessageId),("@now",clock.GetUtcNow().UtcDateTime));
   save.Parameters["@logical"].SqlDbType=System.Data.SqlDbType.UniqueIdentifier;
   await using(var tx=(SqlTransaction)await connection.BeginTransactionAsync(budget.Token)){save.Transaction=tx;await save.ExecuteNonQueryAsync(budget.Token);await tx.CommitAsync(budget.Token);}
   // No body, receipt/lock token, arbitrary transport header or provider data leaves this API.
   result.Add(new(id,source,message.SequenceNumber,envelope?.MessageId,envelope?.Type??"InvalidEnvelope",envelope?.SagaId,
    SafeReason(message.DeadLetterReason),envelope is not null));
  }
  await Audit(connection,actor,"DlqPeek:"+source,null,budget.Token);return result.ToArray();
 }
 private static string SafeReason(string? reason)=>reason is "InvalidJson" or "InvalidEnvelope" or "MessageIdentityConflict" or "WrongEntityRoute" or "MaxDeliveryCountExceeded" or "TTLExpiredException"?"Broker:"+reason:"ClassifiedPoison";
 public async Task<ReplayView> ReplayAsync(Guid id,string actor,CancellationToken ct,Func<CancellationToken,Task>? afterSend=null) {
  RequireLab();_=Broker;using var budget=CancellationTokenSource.CreateLinkedTokenSource(ct);budget.CancelAfter(TimeSpan.FromSeconds(25));
  string source;long sequence;string hash;var ownerId=Guid.NewGuid();
  await using(var connection=await Open(budget.Token)){
   await using var tx=(SqlTransaction)await connection.BeginTransactionAsync(budget.Token);
   using var claim=Command(connection,$"SELECT Source,SequenceNumber,BodyHash,Status,LeaseUntilUtc,InspectedAtUtc FROM [{Schema}].[DlqReplays] WITH(UPDLOCK,HOLDLOCK) WHERE Id=@id",("@id",id));
   claim.Transaction=tx;await using var reader=await claim.ExecuteReaderAsync(budget.Token);
   if(!await reader.ReadAsync(budget.Token))throw new OperationsProblem(404,"InspectionNotFound");
   source=reader.GetString(0);sequence=reader.GetInt64(1);hash=reader.GetString(2);
   if(reader.GetString(3)=="Completed")return new(id,"SentAndCompleted");
   if(!reader.IsDBNull(4)&&Utc(reader,4)>clock.GetUtcNow())throw new OperationsProblem(409,"ReplayInProgress");
   if(Utc(reader,5)<clock.GetUtcNow().AddMinutes(-5))throw new OperationsProblem(409,"InspectionExpired");
   await reader.DisposeAsync();
   using var update=Command(connection,$"UPDATE [{Schema}].[DlqReplays] SET Status='Replaying',LeaseOwner=@owner,LeaseUntilUtc=@until WHERE Id=@id",("@owner",ownerId),("@until",clock.GetUtcNow().AddSeconds(60).UtcDateTime),("@id",id));
   update.Transaction=tx;await update.ExecuteNonQueryAsync(budget.Token);
   await Audit(connection,actor,"ReplayStarted",id,budget.Token,tx);await tx.CommitAsync(budget.Token);
  }
  var outcome="ReplayFailed";var held=new List<ServiceBusReceivedMessage>();
  await using var receiver=Receiver(source);
  try {
   ServiceBusReceivedMessage? target=null;
   // Service Bus does not support receiving an arbitrary active DLQ sequence by ID.
   // Hold at most 50 locks while locating the inspected item, then abandon unrelated items.
   for(var scanned=0;scanned<50&&target is null;){
    var batch=await receiver.ReceiveMessagesAsync(Math.Min(10,50-scanned),TimeSpan.FromSeconds(1),budget.Token);
    if(batch.Count==0)break;
    scanned+=batch.Count;
    foreach(var message in batch){held.Add(message);if(message.SequenceNumber==sequence)target=message;}
   }
   if(target is null)throw new OperationsProblem(409,"DlqItemUnavailableWithinBound");
   if(Convert.ToHexString(SHA256.HashData(target.Body.ToMemory().Span))!=hash)throw new OperationsProblem(409,"InspectionContentChanged");
   var envelope=ReplayEnvelope(target,source)??throw new OperationsProblem(409,"EnvelopeUnsafeForReplay");
   var destination=source=="events"?MessageRoutes.Topic:BusTopology.CommandQueue(Schema);
   await Broker.SendReplayAsync(target.Body.ToString(),destination,id,budget.Token);
   outcome="SentSettlementUncertain";
   await using(var connection=await Open(budget.Token))await Audit(connection,actor,"ReplaySendAccepted",id,budget.Token);
   if(afterSend is not null)await afterSend(budget.Token);
   await receiver.CompleteMessageAsync(target,budget.Token);held.Remove(target);outcome="Completed";
   LabTelemetry.Count("dlq_replay","sent");return new(id,"SentAndCompleted");
  }catch(ServiceBusException){throw new OperationsProblem(503,outcome);}
  finally {
   using var releaseBudget=new CancellationTokenSource(TimeSpan.FromSeconds(5));
   foreach(var message in held){try{await receiver.AbandonMessageAsync(message,cancellationToken:releaseBudget.Token);}catch(ServiceBusException){}catch(OperationCanceledException){}}
   using var cleanup=new CancellationTokenSource(TimeSpan.FromSeconds(5));
   await using var connection=await Open(cleanup.Token);
   using var update=Command(connection,$"UPDATE [{Schema}].[DlqReplays] SET Status=@status,LeaseOwner=NULL,LeaseUntilUtc=NULL WHERE Id=@id AND LeaseOwner=@owner",
    ("@status",outcome),("@id",id),("@owner",ownerId));
   await update.ExecuteNonQueryAsync(cleanup.Token);await Audit(connection,actor,outcome,id,cleanup.Token);
  }
 }
}
