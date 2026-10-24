using Azure.Messaging.ServiceBus;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using TourLab.Azure.Contracts;
using TourLab.Azure.Infrastructure.Messaging;
using TourLab.Azure.Infrastructure.Operations;
using TourLab.Azure.Infrastructure.Persistence;
namespace TourLab.Azure.Tests;
[Collection("SQL Identity")]
public sealed class BrokerRecoveryTests {
 private sealed class Development:IHostEnvironment {
  public string EnvironmentName{get;set;}=Environments.Development;public string ApplicationName{get;set;}="RecoveryTests";
  public string ContentRootPath{get;set;}=TestSettings.Root;public IFileProvider ContentRootFileProvider{get;set;}=new NullFileProvider();
 }
 private static ServiceProvider Provider(string schema,TimeProvider? clock=null){
  var config=new ConfigurationBuilder().AddJsonFile(Path.Combine(TestSettings.Output,schema+".json")).Build();
  var services=new ServiceCollection();services.AddSingleton<IHostEnvironment>(new Development());services.AddSingleton(clock??TimeProvider.System);
  services.AddSingleton(new OperationsOwner(schema,true));services.AddSingleton<WorkerStatus>();
  services.Configure<RuntimeDatabase>(o=>{o.ConnectionString=TestSettings.Configuration.Runtime[schema];o.Schema=schema;o.Development=true;});
  services.AddLogging();services.AddScoped(_=>BookingSqlTests.Catalog());services.AddScoped<TourLab.Azure.Catalog.Application.InventoryHandlers>();services.AddScoped<IInboxConsumer,TourLab.Azure.Catalog.Messaging.CatalogConsumer>();
  services.Configure<MessagingOptions>(config.GetSection("Messaging"));services.AddSingleton<ServiceBusTransport>();services.AddScoped<ServiceOperations>();
  return services.BuildServiceProvider();
 }
 [BrokerFact]
 public async Task ActualTopicDlqReplayCrashRetainsOriginalAndConcurrentLeasePreventsRacesWhileLogicalInboxDeduplicates(){
  var clock=new BookingSqlTests.Clock();await using var provider=Provider("reservations",clock);await using var scope=provider.CreateAsyncScope();
  var operations=scope.ServiceProvider.GetRequiredService<ServiceOperations>();var transport=provider.GetRequiredService<ServiceBusTransport>();
  var id=Guid.NewGuid();var session=Guid.NewGuid();var delivery=Guid.NewGuid();
  var body=MessageRoutes.Serialize(new TourSessionChanged(session,"Synthetic replay evidence",true,1,12345,"MXN"),id,delivery,DateTimeOffset.UtcNow,null,id,id);
  await using var active=transport.Client.CreateReceiver(MessageRoutes.Topic,"reservations-events");
  using var budget=new CancellationTokenSource(TimeSpan.FromSeconds(50));
  await transport.SendAsync(new(delivery,id,MessageRoutes.Topic,body),budget.Token);
  var original=await active.ReceiveMessageAsync(TimeSpan.FromSeconds(5),budget.Token);Assert.NotNull(original);
  await active.DeadLetterMessageAsync(original,"ControlledReplayBoundary",cancellationToken:budget.Token);
  var inspected=Assert.Single(await operations.InspectAsync("events",10,original.SequenceNumber,"test-admin",budget.Token),x=>x.SequenceNumber==original.SequenceNumber);
  Assert.True(inspected.Replayable);Assert.Equal(id,inspected.LogicalMessageId);
  clock.Now=clock.Now.AddMinutes(6);
  var expired=await Assert.ThrowsAsync<OperationsProblem>(()=>operations.ReplayAsync(inspected.Id,"test-admin",budget.Token));
  Assert.Equal("InspectionExpired",expired.Code);
  var refreshed=Assert.Single(await operations.InspectAsync("events",10,original.SequenceNumber,"test-admin",budget.Token),x=>x.SequenceNumber==original.SequenceNumber);
  Assert.Equal(inspected.Id,refreshed.Id);
  await using var dlq=transport.Client.CreateReceiver(MessageRoutes.Topic,"reservations-events",new ServiceBusReceiverOptions{SubQueue=SubQueue.DeadLetter});
  Assert.Equal(original.SequenceNumber,(await dlq.PeekMessageAsync(original.SequenceNumber,budget.Token))!.SequenceNumber);
  var entered=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);var release=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
  var first=operations.ReplayAsync(inspected.Id,"test-admin",budget.Token,async ct=>{entered.SetResult();await release.Task.WaitAsync(ct);throw new BookingSqlTests.Crash();});
  await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
  try{
   await using var other=provider.CreateAsyncScope();
   var conflict=await Assert.ThrowsAsync<OperationsProblem>(()=>other.ServiceProvider.GetRequiredService<ServiceOperations>().ReplayAsync(inspected.Id,"other-admin",budget.Token));
   Assert.Equal(409,conflict.Status);Assert.Equal("ReplayInProgress",conflict.Code);
  }finally{release.SetResult();}
  await Assert.ThrowsAsync<BookingSqlTests.Crash>(()=>first);
  Assert.Equal(original.SequenceNumber,(await dlq.PeekMessageAsync(original.SequenceNumber,budget.Token))!.SequenceNumber);
  var resent=await active.ReceiveMessageAsync(TimeSpan.FromSeconds(5),budget.Token);Assert.NotNull(resent);
  Assert.NotEqual(original.MessageId,resent.MessageId);Assert.Equal(id.ToString("D"),resent.ApplicationProperties["originalLogicalMessageId"]);
  await using(var db=BookingSqlTests.Reservations())await new TourLab.Azure.Reservations.Messaging.ReservationConsumer(db,TimeProvider.System).ConsumeAsync(MessageCodec.Parse(resent.Body.ToString()),budget.Token);
  await active.CompleteMessageAsync(resent,budget.Token);
  Assert.Equal("SentAndCompleted",(await operations.ReplayAsync(inspected.Id,"test-admin",budget.Token)).Outcome);
  var repeated=await active.ReceiveMessageAsync(TimeSpan.FromSeconds(5),budget.Token);Assert.NotNull(repeated);
  Assert.NotEqual(resent.MessageId,repeated.MessageId);Assert.Equal(id,MessageCodec.Parse(repeated.Body.ToString()).MessageId);
  await using(var db=BookingSqlTests.Reservations())await new TourLab.Azure.Reservations.Messaging.ReservationConsumer(db,TimeProvider.System).ConsumeAsync(MessageCodec.Parse(repeated.Body.ToString()),budget.Token);
  await active.CompleteMessageAsync(repeated,budget.Token);
  Assert.Equal("SentAndCompleted",(await operations.ReplayAsync(inspected.Id,"test-admin",budget.Token)).Outcome);
  Assert.Null(await active.ReceiveMessageAsync(TimeSpan.FromSeconds(1),budget.Token));
  await using(var db=BookingSqlTests.Reservations()){
   Assert.Equal(1,await db.Inbox.CountAsync(x=>x.MessageId==id,budget.Token));Assert.Equal(1,await db.Projections.CountAsync(x=>x.SessionId==session,budget.Token));
   var audit=await db.Set<TourLab.Azure.Reservations.Persistence.OperationsAudit>().Where(x=>x.ReplayId==inspected.Id).ToListAsync(budget.Token);
   Assert.Equal(2,audit.Count(x=>x.Action=="ReplaySendAccepted"));Assert.Contains(audit,x=>x.Action=="SentSettlementUncertain");Assert.Contains(audit,x=>x.Action=="Completed");
  }
 }
 [BrokerFact]
 public async Task ActualMalformedAndOlderThanRetentionMessagesCannotBeReplayedAndStayInBuiltInDlq(){
  await using var provider=Provider("catalog");await using var scope=provider.CreateAsyncScope();
  var operations=scope.ServiceProvider.GetRequiredService<ServiceOperations>();var transport=provider.GetRequiredService<ServiceBusTransport>();
  await using var sender=transport.Client.CreateSender("tourlab-azure-catalog-commands");await using var active=transport.Client.CreateReceiver("tourlab-azure-catalog-commands");
  await using var dlq=transport.Client.CreateReceiver("tourlab-azure-catalog-commands",new ServiceBusReceiverOptions{SubQueue=SubQueue.DeadLetter});
  using var budget=new CancellationTokenSource(TimeSpan.FromSeconds(35));
  foreach(var body in new[]{"{invalid",MessageRoutes.Serialize(new HoldSeats(Guid.NewGuid(),Guid.NewGuid(),Guid.NewGuid(),"synthetic"),Guid.NewGuid(),Guid.NewGuid(),DateTimeOffset.UtcNow.AddDays(-15),Guid.NewGuid(),Guid.NewGuid(),Guid.NewGuid())}){
   var message=new ServiceBusMessage(body){MessageId=Guid.NewGuid().ToString("D"),Subject="HoldSeats"};message.ApplicationProperties["type"]="HoldSeats";
   await sender.SendMessageAsync(message,budget.Token);var original=await active.ReceiveMessageAsync(TimeSpan.FromSeconds(5),budget.Token);Assert.NotNull(original);Assert.Equal(message.MessageId,original.MessageId);
   await active.DeadLetterMessageAsync(original,"ControlledUnsafeReplay",cancellationToken:budget.Token);
   var inspected=Assert.Single(await operations.InspectAsync("commands",10,original.SequenceNumber,"test-admin",budget.Token),x=>x.SequenceNumber==original.SequenceNumber);
   Assert.False(inspected.Replayable);
   var denied=await Assert.ThrowsAsync<OperationsProblem>(()=>operations.ReplayAsync(inspected.Id,"test-admin",budget.Token));
   Assert.Equal(409,denied.Status);Assert.Equal("EnvelopeUnsafeForReplay",denied.Code);
   Assert.Equal(original.SequenceNumber,(await dlq.PeekMessageAsync(original.SequenceNumber,budget.Token))!.SequenceNumber);
  }
 }
 [BrokerFact]
 public async Task ActualSdkProcessorStopsGracefullyOnCancellationWithNoReceiveAndDelete(){
  await using var provider=Provider("catalog");var status=provider.GetRequiredService<WorkerStatus>();
  var worker=new InboxWorker(provider.GetRequiredService<IServiceScopeFactory>(),provider.GetRequiredService<IOptions<MessagingOptions>>(),
   provider.GetRequiredService<ServiceBusTransport>(),"catalog",Microsoft.Extensions.Logging.Abstractions.NullLogger<InboxWorker>.Instance,status);
  using var stop=new CancellationTokenSource();var running=worker.RunAsync(stop.Token);
  try{
   using var ready=new CancellationTokenSource(TimeSpan.FromSeconds(10));
   while(!status.Snapshot().Any(x=>x.Name=="inbox"&&x.Status=="Running"))await Task.Delay(20,ready.Token);
  }finally{await stop.CancelAsync();}
  await running.WaitAsync(TimeSpan.FromSeconds(8));Assert.Contains(status.Snapshot(),x=>x.Name=="inbox"&&x.Status=="Stopped");
 }
}
