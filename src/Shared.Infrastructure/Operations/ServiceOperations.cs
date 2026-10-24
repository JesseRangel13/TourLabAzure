using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using TourLab.Azure.Infrastructure.Persistence;
namespace TourLab.Azure.Infrastructure.Operations;
public sealed class OperationsProblem(int status,string code):Exception(code) { public int Status{get;}=status; public string Code{get;}=code; }
public sealed record FaultSelection(string Mode,int Occurrences);
public sealed record FaultView(string Mode,int Remaining);
public sealed record OperationsOwner(string Schema,bool LabEnabled);
public sealed partial class ServiceOperations(IOptions<RuntimeDatabase> database,OperationsOwner owner,WorkerStatus workers,TimeProvider clock,
 IServiceProvider services) {
 private string Schema=>SqlRules.Schema(owner.Schema);
 public bool LabEnabled=>owner.LabEnabled;
 internal async Task<SqlConnection> Open(CancellationToken ct) {
  var connection=new SqlConnection(database.Value.ConnectionString);
  try{await connection.OpenAsync(ct);return connection;}catch{await connection.DisposeAsync();throw;}
 }
 internal static SqlCommand Command(SqlConnection connection,string text,params (string Name,object? Value)[] values) {
  var command=new SqlCommand(text,connection){CommandTimeout=5};
  foreach(var (name,value) in values)command.Parameters.AddWithValue(name,value??DBNull.Value);
  return command;
 }
 internal async Task Audit(SqlConnection connection,string actor,string action,Guid? id,CancellationToken ct,SqlTransaction? tx=null) {
  using var command=Command(connection,$"INSERT INTO [{Schema}].[OperationsAudit] (Id,Actor,Action,ReplayId,OccurredAtUtc) VALUES (@id,@actor,@action,@replay,@now)",
   ("@id",Guid.NewGuid()),("@actor",actor),("@action",action),("@replay",id),("@now",clock.GetUtcNow().UtcDateTime));
  command.Parameters["@replay"].SqlDbType=System.Data.SqlDbType.UniqueIdentifier;
  command.Transaction=tx;await command.ExecuteNonQueryAsync(ct);
 }
 public void RequireLab(){if(!LabEnabled)throw new OperationsProblem(404,"LabFeaturesDisabled");}
 private string[] Modes=>Schema switch {
  "catalog"=>["None","QuoteTimeout","RejectNextConfirmation","PauseConsumption"],
  "reservations"=>["None","PauseOutbox"],
  "payments"=>["Success","Decline","TimeoutAfterCharge","UnknownUntilAdminResolution","RefundTransientFailure","PauseConsumption"],
  "notifications"=>["None","FailureThenSuccess"], _=>[] };
 public async Task<FaultView> FaultAsync(FaultSelection? selection,string actor,CancellationToken ct) {
  RequireLab();await using var connection=await Open(ct);
  if(selection is not null){
   if(!Modes.Contains(selection.Mode,StringComparer.Ordinal)||selection.Occurrences is <0 or >100)throw new OperationsProblem(400,"InvalidFaultSelection");
   await using var tx=(SqlTransaction)await connection.BeginTransactionAsync(ct);
   using var update=Command(connection,$"UPDATE [{Schema}].[Faults] SET Mode=@mode,Remaining=@count WHERE Id=1",("@mode",selection.Mode),("@count",selection.Occurrences));
   update.Transaction=tx;await update.ExecuteNonQueryAsync(ct);
   await Audit(connection,actor,"Simulated:"+selection.Mode+":"+selection.Occurrences,null,ct,tx);await tx.CommitAsync(ct);
  }
  using var read=Command(connection,$"SELECT Mode,Remaining FROM [{Schema}].[Faults] WHERE Id=1");
  await using var reader=await read.ExecuteReaderAsync(ct);await reader.ReadAsync(ct);return new(reader.GetString(0),reader.GetInt32(1));
 }
 public async Task<bool> TakeFaultAsync(string mode,CancellationToken ct) {
  if(!LabEnabled)return false;
  await using var connection=await Open(ct);await using var tx=(SqlTransaction)await connection.BeginTransactionAsync(ct);
  using var command=Command(connection,$"UPDATE [{Schema}].[Faults] WITH (UPDLOCK) SET Remaining=Remaining-1 OUTPUT inserted.Id WHERE Id=1 AND Mode=@mode AND Remaining>0",("@mode",mode));
  command.Transaction=tx;var taken=await command.ExecuteScalarAsync(ct) is not null;
  if(taken)await Audit(connection,"worker","FaultOccurrence:"+mode,null,ct,tx);
  await tx.CommitAsync(ct);return taken;
 }
 public async Task<object> DiagnosticsAsync(int page,int size,CancellationToken ct) {
  if(page<1||size is <1 or >50||(long)(page-1)*size>int.MaxValue)throw new OperationsProblem(400,"InvalidPagination");
  await using var connection=await Open(ct);
  long pending;double age;
  using(var command=Command(connection,$"SELECT COUNT_BIG(*),COALESCE(DATEDIFF_BIG(second,MIN(OccurredAtUtc),SYSUTCDATETIME()),0) FROM [{Schema}].[Outbox] WHERE PublishedAtUtc IS NULL"))
  await using(var reader=await command.ExecuteReaderAsync(ct)){await reader.ReadAsync(ct);pending=reader.GetInt64(0);age=Math.Max(0,reader.GetInt64(1));}
  var outbox=new List<object>();var inbox=new List<object>();var audit=new List<object>();var states=new Dictionary<string,long>();
  using(var command=Command(connection,$"SELECT DeliveryId,MessageId,Type,Destination,Attempts,OccurredAtUtc,PublishedAtUtc,LeaseUntilUtc FROM [{Schema}].[Outbox] ORDER BY OccurredAtUtc DESC,DeliveryId OFFSET @offset ROWS FETCH NEXT @size ROWS ONLY",("@offset",(page-1)*size),("@size",size)))
  await using(var reader=await command.ExecuteReaderAsync(ct)){while(await reader.ReadAsync(ct))outbox.Add(new{deliveryId=reader.GetGuid(0),messageId=reader.GetGuid(1),type=reader.GetString(2),destination=reader.GetString(3),attempts=reader.GetInt32(4),occurredAtUtc=Utc(reader,5),publishedAtUtc=NullableUtc(reader,6),leaseUntilUtc=NullableUtc(reader,7)});}
  using(var command=Command(connection,$"SELECT ConsumerName,MessageId,ProcessedAtUtc FROM [{Schema}].[Inbox] ORDER BY ProcessedAtUtc DESC,MessageId OFFSET @offset ROWS FETCH NEXT @size ROWS ONLY",("@offset",(page-1)*size),("@size",size)))
  await using(var reader=await command.ExecuteReaderAsync(ct)){while(await reader.ReadAsync(ct))inbox.Add(new{consumerName=reader.GetString(0),messageId=reader.GetGuid(1),processedAtUtc=Utc(reader,2)});}
  using(var command=Command(connection,$"SELECT Actor,Action,ReplayId,OccurredAtUtc FROM [{Schema}].[OperationsAudit] ORDER BY OccurredAtUtc DESC,Id OFFSET @offset ROWS FETCH NEXT @size ROWS ONLY",("@offset",(page-1)*size),("@size",size)))
  await using(var reader=await command.ExecuteReaderAsync(ct)){while(await reader.ReadAsync(ct))audit.Add(new{actor=reader.GetString(0),action=reader.GetString(1),replayId=reader.IsDBNull(2)?(Guid?)null:reader.GetGuid(2),occurredAtUtc=Utc(reader,3)});}
  var tables=Schema switch{"catalog"=>new[]{("Holds","Status","hold")},"reservations"=>[("Sagas","State","saga")],"payments"=>[("Operations","Status","payment"),("RefundOperations","Status","refund")],_=>[("Notifications","Status","notification")]};
  foreach(var (table,column,prefix) in tables){
   using var command=Command(connection,$"SELECT TOP(20) [{column}],COUNT_BIG(*) FROM [{Schema}].[{table}] GROUP BY [{column}]");
   await using var reader=await command.ExecuteReaderAsync(ct);while(await reader.ReadAsync(ct))states[prefix+":"+reader.GetString(0)]=reader.GetInt64(1);
  }
  LabTelemetry.Measure("outbox.pending",pending);LabTelemetry.Measure("outbox.age_seconds",age);
  return new{ignoredDuplicates=LabTelemetry.IgnoredDuplicates,service=Schema,pendingOutboxCount=pending,oldestPendingOutboxAgeSeconds=age,states,workers=workers.Snapshot(),outbox,inbox,audit,page,pageSize=size};
 }
 private static DateTimeOffset Utc(SqlDataReader reader,int ordinal)=>new(DateTime.SpecifyKind(reader.GetDateTime(ordinal),DateTimeKind.Utc));
 private static DateTimeOffset? NullableUtc(SqlDataReader reader,int ordinal)=>reader.IsDBNull(ordinal)?null:Utc(reader,ordinal);
}
