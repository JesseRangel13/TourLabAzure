using System.Net;
using System.Net.Http.Headers;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using TourLab.Azure.Contracts;
using TourLab.Azure.Infrastructure.Messaging;
using TourLab.Azure.Infrastructure.Operations;
using TourLab.Azure.Infrastructure.Persistence;
using TourLab.Azure.Payments.Application;
using TourLab.Azure.Reservations.Application;
using TourLab.Azure.Reservations.Persistence;
using TourLab.Azure.Reservations.Messaging;
using TourLab.Azure.Catalog.Messaging;
using TourLab.Azure.Catalog.Application;
namespace TourLab.Azure.Tests;

[Collection("SQL Identity")]
public sealed class RecoverySqlTests {
 internal sealed class Flow:IAsyncDisposable {
  public readonly BookingSqlTests.Clock Clock=new();
  private readonly ServiceProvider services=new ServiceCollection().BuildServiceProvider();
  public Guid Id;public Guid SagaId;public TourLab.Azure.Catalog.Application.SessionView Session=null!;
  public ServiceOperations Ops(string schema)=>new(Options.Create(new RuntimeDatabase{ConnectionString=TestSettings.Configuration.Runtime[schema],Schema=schema,Development=true}),new(schema,true),new(),Clock,services);
  public async Task Init(string mode="Success"){
   Session=await BookingSqlTests.Session();
   await using var quote=new BookingSqlTests.QuoteFixture();
   await using(var db=BookingSqlTests.Reservations()){
    var accepted=await new ReservationCreator(db,quote.Client,Clock).CreateAsync("recovery-owner",Guid.NewGuid().ToString(),new(Session.Id,2,12345,"MXN"),default);
    Id=accepted.ResourceId;SagaId=JsonDocument.Parse(accepted.ResponseBody).RootElement.GetProperty("sagaId").GetGuid();
   }
   await Ops("payments").FaultAsync(new(mode,1),"test-admin",default);
  }
  public async Task<ReservationSaga> Saga(){await using var db=BookingSqlTests.Reservations();return await db.Sagas.AsNoTracking().SingleAsync(x=>x.Id==SagaId);}
  public async Task<IntegrationEnvelope<JsonElement>> Intent(string schema,string type){
   await using DbContext db=schema switch{"catalog"=>BookingSqlTests.Catalog(),"reservations"=>BookingSqlTests.Reservations(),_=>BookingSqlTests.Payments()};
   var sql=$"SELECT EnvelopeJson AS [Value] FROM [{SqlRules.Schema(schema)}].[Outbox] WHERE Type={{0}}";
   var rows=await db.Database.SqlQueryRaw<string>(sql,type).ToListAsync();
   return Assert.Single(rows.Select(MessageCodec.Parse),x=>x.SagaId==SagaId);
  }
  public async Task Consume(string schema,IntegrationEnvelope<JsonElement> envelope){
   using var activity=TourLab.Azure.Infrastructure.LabTelemetry.Start("test.consumer",System.Diagnostics.ActivityKind.Consumer,envelope.Traceparent,envelope.Tracestate);
   if(schema=="catalog"){await using var db=BookingSqlTests.Catalog();await new CatalogConsumer(db,new InventoryHandlers(db,Clock,Ops("catalog"))).ConsumeAsync(envelope,default);}
   else if(schema=="reservations"){await using var db=BookingSqlTests.Reservations();await new ReservationConsumer(db,Clock).ConsumeAsync(envelope,default);}
   else {await using var db=BookingSqlTests.Payments();await new PaymentConsumer(db).ConsumeAsync(envelope,default);}
  }
  public Task Pass(string from,string to,string type)=>PassCore(from,to,type);
  private async Task PassCore(string from,string to,string type)=>await Consume(to,await Intent(from,type));
  public async Task Held(){
   await Pass("reservations","catalog","HoldSeats");await Pass("catalog","reservations","SeatsHeld");await Pass("reservations","payments","ProcessPayment");
  }
  public DurableFakeProvider Provider()=>new(new BookingSqlTests.Factory<TourLab.Azure.Payments.Persistence.PaymentsDb>(BookingSqlTests.Payments),Clock);
  public async Task<bool> Pay(Func<CancellationToken,Task>? barrier=null){
   await using var db=BookingSqlTests.Payments();return await new PaymentProcessor(db,Provider(),Clock).ProcessAsync(default,(await Saga()).PaymentOperationId,barrier);
  }
  public async Task Paid(){await Held();Assert.True(await Pay());await Pass("payments","reservations","PaymentSucceeded");}
  public async Task Confirmed(){await Paid();await Pass("reservations","catalog","ConfirmSeats");await Pass("catalog","reservations","SeatsConfirmed");Assert.Equal("Confirmed",(await Saga()).State);}
  public async Task Cancel(string key){
   await using var db=BookingSqlTests.Reservations();await new SagaCommands(db,new SagaRecovery(db,Clock,new()),Clock).CancelAsync(Id,"recovery-owner",false,key,default);
  }
  public async Task Due(){
   await using var db=BookingSqlTests.Reservations();Assert.True(await new SagaRecovery(db,Clock,new()).DueAsync(default,SagaId));
  }
  public async Task Release(){await Pass("reservations","catalog","ReleaseSeats");await Pass("catalog","reservations","SeatsReleased");}
  public async Task Refund(Func<CancellationToken,Task>? barrier=null){
   await Pass("reservations","payments","RefundPayment");
   await using var db=BookingSqlTests.Payments();Assert.True(await new RefundProcessor(db,Provider(),Clock).ProcessAsync(default,(await Saga()).RefundOperationId,barrier));
  }
  public async Task Stock(int expected){await using var db=BookingSqlTests.Catalog();Assert.Equal(expected,(await db.Sessions.SingleAsync(x=>x.Id==Session.Id)).AvailableSeats);}
  public async ValueTask DisposeAsync(){
   foreach(var schema in new[]{"catalog","reservations","payments","notifications"})await Ops(schema).FaultAsync(new(schema=="payments"?"Success":"None",0),"test-admin",default);
   await services.DisposeAsync();
  }
 }
 internal static HttpClient Client<T>(Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<T> factory,string service,string role="Admin",string subject="recovery-owner") where T:class {
  var client=factory.CreateClient();using var rsa=RSA.Create();rsa.ImportFromPem(File.ReadAllText(Path.Combine(TestSettings.Output,"human-signing.key")));
  var now=DateTime.UtcNow.AddSeconds(-1);
  var token=new JwtSecurityToken(TourLab.Azure.Infrastructure.Security.BackendSecurity.Issuer,TourLab.Azure.Infrastructure.Security.BackendSecurity.Audience(service),
   [new Claim("sub",subject),new Claim("role",role)],now,now.AddMinutes(2),new SigningCredentials(new RsaSecurityKey(rsa){CryptoProviderFactory=new(){CacheSignatureProviders=false}},SecurityAlgorithms.RsaSha256));
  client.DefaultRequestHeaders.Authorization=new AuthenticationHeaderValue("Bearer",new JwtSecurityTokenHandler().WriteToken(token));return client;
 }
 [SqlTheory][InlineData(true)][InlineData(false)]
 public async Task CancellationRequiresBothResultsInEitherOrderAndDifferentKeysNeverCreateAnotherRefund(bool refundFirst){
  await using var flow=new Flow();await flow.Init();await flow.Confirmed();var before=await flow.Saga();
  await Task.WhenAll(flow.Cancel(Guid.NewGuid().ToString()),flow.Cancel(Guid.NewGuid().ToString()));
  Assert.Equal("CancellationPending",(await flow.Saga()).State);
  await flow.Refund();
  if(refundFirst){await flow.Pass("payments","reservations","PaymentRefunded");Assert.Equal("CancellationPending",(await flow.Saga()).State);await flow.Release();}
  else {await flow.Release();Assert.Equal("CancellationPending",(await flow.Saga()).State);await flow.Pass("payments","reservations","PaymentRefunded");}
  await flow.Cancel(Guid.NewGuid().ToString());var after=await flow.Saga();
  Assert.Equal("Cancelled",after.State);Assert.Equal(before.RefundOperationId,after.RefundOperationId);
  await using(var db=BookingSqlTests.Payments())Assert.Equal(1,await db.ProviderRefunds.CountAsync(x=>x.PaymentOperationId==before.PaymentOperationId));
  await flow.Stock(6);
 }
 [SqlFact]
 public async Task DeclineReleasesBeforeFailureAndNeverRefunds(){
  await using var flow=new Flow();await flow.Init("Decline");await flow.Held();Assert.True(await flow.Pay());
  await flow.Pass("payments","reservations","PaymentDeclined");Assert.Equal("Compensating",(await flow.Saga()).State);
  await flow.Release();Assert.Equal("Failed",(await flow.Saga()).State);await flow.Stock(6);
  await using var db=BookingSqlTests.Payments();Assert.False(await db.Refunds.AnyAsync(x=>x.SagaId==flow.SagaId));
 }
 [SqlFact]
 public async Task ConfirmationRejectionRefundsAndReleasesAndDuplicateConfirmationCannotResurrect(){
  await using var flow=new Flow();await flow.Init();await flow.Paid();await flow.Ops("catalog").FaultAsync(new("RejectNextConfirmation",1),"test-admin",default);
  await flow.Pass("reservations","catalog","ConfirmSeats");await flow.Pass("catalog","reservations","SeatsConfirmationRejected");
  Assert.Equal("Compensating",(await flow.Saga()).State);await flow.Refund();await flow.Release();
  Assert.Equal("Compensating",(await flow.Saga()).State);await flow.Pass("payments","reservations","PaymentRefunded");
  Assert.Equal("Failed",(await flow.Saga()).State);await flow.Stock(6);
  var duplicate=await flow.Intent("reservations","ConfirmSeats");
  await flow.Consume("catalog",duplicate with{MessageId=Guid.NewGuid(),DeliveryId=Guid.NewGuid()});
  Assert.Equal("Failed",(await flow.Saga()).State);await flow.Stock(6);
 }
 [SqlFact]
 public async Task AvailabilityDeadlineAndLateHeldNeverStartPaymentAndReleaseBeforeHoldLeavesTombstone(){
  await using var flow=new Flow();await flow.Init();flow.Clock.Now=flow.Clock.Now.AddSeconds(31);await flow.Due();await flow.Release();
  var saga=await flow.Saga();Assert.Equal("Failed",saga.State);
  await flow.Pass("reservations","catalog","HoldSeats");await flow.Stock(6);
  await flow.Consume("reservations",BookingSqlTests.Envelope(new SeatsHeld(saga.HoldId,flow.Clock.Now.AddMinutes(10)),saga.Id));
  Assert.False((await flow.Saga()).PaymentRequested);
  await using var db=BookingSqlTests.Payments();Assert.False(await db.Operations.AnyAsync(x=>x.SagaId==saga.Id));
 }
 [SqlFact]
 public async Task ConfirmationTimerAndLateResultRaceNeverConfirmAbandonedSeats(){
  await using var flow=new Flow();await flow.Init();await flow.Paid();var saga=await flow.Saga();flow.Clock.Now=flow.Clock.Now.AddSeconds(31);
  var result=BookingSqlTests.Envelope(new SeatsConfirmed(saga.HoldId),saga.Id);
  async Task Timer(){await using var db=BookingSqlTests.Reservations();await new SagaRecovery(db,flow.Clock,new()).DueAsync(default,flow.SagaId);}
  await Task.WhenAll(Timer(),flow.Consume("reservations",result));
  Assert.Equal("Compensating",(await flow.Saga()).State);
  await using var db=BookingSqlTests.Reservations();
  Assert.Equal(1,await db.Outbox.CountAsync(x=>x.EffectKey==$"saga/{saga.Id}/refund"));
  Assert.Equal(1,await db.Outbox.CountAsync(x=>x.EffectKey==$"saga/{saga.Id}/release"));
 }
 [SqlFact]
 public async Task ConfirmationWinningBeforeDeadlineMakesTimerNoOp(){
  await using var flow=new Flow();await flow.Init();await flow.Confirmed();flow.Clock.Now=flow.Clock.Now.AddSeconds(31);
  await using var db=BookingSqlTests.Reservations();Assert.False(await new SagaRecovery(db,flow.Clock,new()).DueAsync(default,flow.SagaId));
  Assert.False((await flow.Saga()).RefundRequired);
 }
 [SqlFact]
 public async Task TwoPaymentWorkersRaceOneDurableChargeWhileFirstOwnsLease(){
  await using var flow=new Flow();await flow.Init();await flow.Held();var entered=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
  var release=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
  var first=flow.Pay(async ct=>{entered.SetResult();await release.Task.WaitAsync(ct);});
  await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
  try{Assert.False(await flow.Pay());}finally{release.SetResult();}
  Assert.True(await first);
  var saga=await flow.Saga();await using var db=BookingSqlTests.Payments();Assert.Equal(1,await db.Effects.CountAsync(x=>x.Id==saga.PaymentOperationId));
 }
 [SqlFact]
 public async Task RefundTransientFailuresAndProviderCommitBeforeCrashRecoverWithOneRefund(){
  await using var flow=new Flow();await flow.Init("RefundTransientFailure");await flow.Confirmed();await flow.Cancel(Guid.NewGuid().ToString());
  await flow.Pass("reservations","payments","RefundPayment");var saga=await flow.Saga();
  for(var attempt=0;attempt<2;attempt++){
   await using var db=BookingSqlTests.Payments();Assert.True(await new RefundProcessor(db,flow.Provider(),flow.Clock).ProcessAsync(default,saga.RefundOperationId));
   Assert.Equal("Pending",(await db.Refunds.AsNoTracking().SingleAsync(x=>x.Id==saga.RefundOperationId)).Status);flow.Clock.Now=flow.Clock.Now.AddSeconds(31);
  }
  await using(var db=BookingSqlTests.Payments())await Assert.ThrowsAsync<BookingSqlTests.Crash>(()=>new RefundProcessor(db,flow.Provider(),flow.Clock).ProcessAsync(default,saga.RefundOperationId,_=>throw new BookingSqlTests.Crash()));
  flow.Clock.Now=flow.Clock.Now.AddSeconds(61);
  await using(var db=BookingSqlTests.Payments())Assert.True(await new RefundProcessor(db,flow.Provider(),flow.Clock).ProcessAsync(default,saga.RefundOperationId));
  await flow.Release();await flow.Pass("payments","reservations","PaymentRefunded");Assert.Equal("Cancelled",(await flow.Saga()).State);await flow.Stock(6);
  await using(var db=BookingSqlTests.Payments()){var receipt=await db.ProviderRefunds.SingleAsync(x=>x.Id==saga.RefundOperationId);Assert.Equal(2,receipt.Failures);Assert.NotNull(receipt.Reference);}
 }
 private sealed class UncertainRefund(IPaymentProvider inner):IPaymentProvider {
  public Task<ProviderCharge> ChargeAsync(TourLab.Azure.Payments.Persistence.PaymentOperation operation,CancellationToken ct)=>inner.ChargeAsync(operation,ct);
  public Task<ProviderCharge?> GetChargeStatusAsync(Guid id,CancellationToken ct)=>inner.GetChargeStatusAsync(id,ct);
  public Task RefundAsync(RefundPayment request,CancellationToken ct)=>throw new TimeoutException("Deterministic uncertain refund boundary");
  public Task<string?> GetRefundStatusAsync(Guid id,CancellationToken ct)=>Task.FromResult<string?>(null);
 }
 [SqlTheory][InlineData("Succeeded")][InlineData("NotCharged")]
 public async Task UnknownExhaustsFiveQueriesRemainsManualReviewThenAdminProviderResolutionReconcilesSameId(string outcome){
  await using var flow=new Flow();await flow.Init("UnknownUntilAdminResolution");await flow.Held();Assert.True(await flow.Pay());
  for(var i=0;i<5;i++){flow.Clock.Now=flow.Clock.Now.AddSeconds(31);Assert.True(await flow.Pay());}
  var saga=await flow.Saga();
  await using(var db=BookingSqlTests.Payments()){
   var operation=await db.Operations.SingleAsync(x=>x.Id==saga.PaymentOperationId);
   Assert.Equal("Unknown",operation.Status);Assert.Equal(5,operation.ReconcileAttempts);
   Assert.Equal(DateTimeOffset.MaxValue,operation.NextReconcileAtUtc);
  }
  await flow.Due();Assert.Equal("PaymentUncertain",(await flow.Saga()).State);await flow.Release();
  flow.Clock.Now=flow.Clock.Now.AddSeconds(121);await flow.Due();Assert.Equal("ManualReview",(await flow.Saga()).State);
  await using var factory=new AzureFactory<TourLab.Azure.Payments.Program>("payments");using var admin=Client(factory,"Payments");
  using(var resolved=await admin.PostAsync($"/v1/admin/fake-provider/{saga.PaymentOperationId}/resolve",System.Net.Http.Json.JsonContent.Create(new{outcome})))Assert.Equal(HttpStatusCode.Accepted,resolved.StatusCode);
  using(var reconciled=await admin.PostAsync($"/v1/admin/payments/{saga.PaymentOperationId}/reconcile",System.Net.Http.Json.JsonContent.Create(new{})))Assert.Equal(HttpStatusCode.Accepted,reconciled.StatusCode);
  Assert.True(await flow.Pay());
  if(outcome=="Succeeded"){
   await flow.Pass("payments","reservations","PaymentSucceeded");Assert.Equal("Compensating",(await flow.Saga()).State);
   await flow.Refund();await flow.Pass("payments","reservations","PaymentRefunded");
  }else await flow.Pass("payments","reservations","PaymentDeclined");
  Assert.Equal("Failed",(await flow.Saga()).State);await flow.Stock(6);
  await using(var db=BookingSqlTests.Payments()){
   Assert.Equal(1,await db.Effects.CountAsync(x=>x.Id==saga.PaymentOperationId));
   Assert.Equal("UnknownUntilAdminResolution",(await db.Operations.SingleAsync(x=>x.Id==saga.PaymentOperationId)).Mode);
   Assert.Equal(outcome=="Succeeded"?1:0,await db.ProviderRefunds.CountAsync(x=>x.PaymentOperationId==saga.PaymentOperationId));
  }
 }
 [SqlFact]
 public async Task UncertainRefundExhaustionAndAdminRetryReuseOperationIdsAndDurableFlags(){
  await using var flow=new Flow();await flow.Init();await flow.Confirmed();await flow.Cancel(Guid.NewGuid().ToString());
  await flow.Pass("reservations","payments","RefundPayment");var saga=await flow.Saga();
  for(var attempt=0;attempt<5;attempt++){
   await using var db=BookingSqlTests.Payments();Assert.True(await new RefundProcessor(db,new UncertainRefund(flow.Provider()),flow.Clock).ProcessAsync(default,saga.RefundOperationId));
   flow.Clock.Now=flow.Clock.Now.AddSeconds(31);
  }
  await flow.Release();await flow.Pass("payments","reservations","RefundNeedsReview");Assert.Equal("ManualReview",(await flow.Saga()).State);
  await using(var db=BookingSqlTests.Reservations()){
   var result=await new SagaCommands(db,new(db,flow.Clock,new()),flow.Clock).RetryAsync(saga.Id,"test-admin",default);
   Assert.Equal(saga.RefundOperationId,result.RefundOperationId);Assert.True(result.SeatsReleased);
   var retry=await db.Outbox.SingleAsync(x=>x.EffectKey.StartsWith($"saga/{saga.Id}/refund-retry/"));
   await flow.Consume("payments",MessageCodec.Parse(retry.EnvelopeJson));
  }
  await using(var db=BookingSqlTests.Payments())Assert.True(await new RefundProcessor(db,flow.Provider(),flow.Clock).ProcessAsync(default,saga.RefundOperationId));
  await flow.Pass("payments","reservations","PaymentRefunded");Assert.Equal("Cancelled",(await flow.Saga()).State);await flow.Stock(6);
 }
 [SqlFact]
 public async Task NotificationTwoTransientFailuresRemainPendingThenSendOnceWithoutRefundingConfirmedBooking(){
  await using var flow=new Flow();await flow.Init();await flow.Confirmed();await flow.Ops("notifications").FaultAsync(new("FailureThenSuccess",1),"test-admin",default);
  var envelope=await flow.Intent("reservations","ReservationConfirmed");
  await using(var db=BookingSqlTests.Notifications())await new TourLab.Azure.Notifications.Application.NotificationConsumer(db,flow.Clock).ConsumeAsync(envelope,default);
  var sender=new TourLab.Azure.Notifications.Application.DurableFakeSender(new BookingSqlTests.Factory<TourLab.Azure.Notifications.Persistence.NotificationsDb>(BookingSqlTests.Notifications),flow.Clock);
  Guid id;await using(var db=BookingSqlTests.Notifications())id=(await db.Notifications.SingleAsync(x=>x.ReservationId==flow.Id)).Id;
  for(var attempt=0;attempt<3;attempt++){
   await using var db=BookingSqlTests.Notifications();Assert.True(await new TourLab.Azure.Notifications.Application.NotificationProcessor(db,sender,flow.Clock).ProcessAsync(default,id));
   Assert.Equal(attempt<2?"Pending":"Sent",(await db.Notifications.AsNoTracking().SingleAsync(x=>x.Id==id)).Status);flow.Clock.Now=flow.Clock.Now.AddSeconds(6);
  }
  await using(var db=BookingSqlTests.Notifications())Assert.Equal(2,(await db.Receipts.SingleAsync(x=>x.Id==id)).Failures);
  Assert.Equal("Confirmed",(await flow.Saga()).State);Assert.False((await flow.Saga()).RefundRequired);
 }
 [SqlFact]
 public async Task TwoDeadlineWorkersClaimOneSagaAndLeaveOneReleaseIntent(){
  await using var flow=new Flow();await flow.Init();flow.Clock.Now=flow.Clock.Now.AddSeconds(31);
  async Task<bool> Due(){await using var db=BookingSqlTests.Reservations();return await new SagaRecovery(db,flow.Clock,new()).DueAsync(default,flow.SagaId);}
  var outcomes=await Task.WhenAll(Due(),Due());Assert.Single(outcomes,x=>x);
  Assert.Equal("Compensating",(await flow.Saga()).State);
  await using var db=BookingSqlTests.Reservations();Assert.Equal(1,await db.Outbox.CountAsync(x=>x.EffectKey==$"saga/{flow.SagaId}/release"));
 }
 [SqlFact]
 public async Task DurableWorkRestoresOriginalW3cTraceAfterTheHttpActivityHasEnded(){
  await using var flow=new Flow();string trace;
  using(var activity=TourLab.Azure.Infrastructure.LabTelemetry.Start("test.http",System.Diagnostics.ActivityKind.Server,null,null)){
   Assert.NotNull(activity);trace=activity.TraceId.ToString();await flow.Init();await flow.Held();
  }
  Assert.True(await flow.Pay());
  var paid=await flow.Intent("payments","PaymentSucceeded");
  Assert.True(System.Diagnostics.ActivityContext.TryParse(paid.Traceparent,paid.Tracestate,out var paidContext));Assert.Equal(trace,paidContext.TraceId.ToString());
  await flow.Pass("payments","reservations","PaymentSucceeded");await flow.Pass("reservations","catalog","ConfirmSeats");await flow.Pass("catalog","reservations","SeatsConfirmed");
  var confirmed=await flow.Intent("reservations","ReservationConfirmed");
  Assert.True(System.Diagnostics.ActivityContext.TryParse(confirmed.Traceparent,confirmed.Tracestate,out var confirmedContext));Assert.Equal(trace,confirmedContext.TraceId.ToString());
  await using(var db=BookingSqlTests.Payments())Assert.NotNull((await db.Operations.SingleAsync(x=>x.SagaId==flow.SagaId)).Traceparent);
 }
 [SqlFact]
 public async Task ExpiredHoldThenActualLateProviderChargeRefundsWithoutConfirmationOrDoubleCapacity(){
  await using var flow=new Flow();await flow.Init();await flow.Held();var saga=await flow.Saga();flow.Clock.Now=flow.Clock.Now.AddMinutes(11);
  await using(var db=BookingSqlTests.Catalog())await new InventoryHandlers(db,flow.Clock).ExpireAsync(saga.HoldId,default);
  await flow.Pass("catalog","reservations","SeatsHoldExpired");Assert.Equal("PaymentUncertain",(await flow.Saga()).State);await flow.Stock(6);
  Assert.True(await flow.Pay());await flow.Pass("payments","reservations","PaymentSucceeded");Assert.Equal("Compensating",(await flow.Saga()).State);
  await flow.Refund();await flow.Pass("payments","reservations","PaymentRefunded");await flow.Release();
  Assert.Equal("Failed",(await flow.Saga()).State);await flow.Stock(6);
  await using var check=BookingSqlTests.Reservations();Assert.False(await check.Outbox.AnyAsync(x=>x.EffectKey==$"saga/{saga.Id}/confirm"));
 }
 [SqlFact]
 public async Task EarlierFailedRecoveryFixtureReopensOnlyRepairForLateDurableChargeAndDoesNotNotifyFailureTwice(){
  // Explicit recovery-handler fixture, matching original W-04: this seeded earlier decision is not E2E evidence.
  await using var flow=new Flow();await flow.Init();await flow.Held();var saga=await flow.Saga();
  await using(var db=BookingSqlTests.Reservations()){
   await using var tx=await db.Database.BeginTransactionAsync();
   var row=(await db.Sagas.FromSqlInterpolated($"SELECT * FROM [reservations].[Sagas] WITH(UPDLOCK,HOLDLOCK) WHERE Id={saga.Id}").ToListAsync()).Single();
   var reservation=await db.Reservations.SingleAsync(x=>x.Id==row.ReservationId);var recovery=new SagaRecovery(db,flow.Clock,new());var from=row.State;
   row.PaymentResolved=true;await recovery.CompensateAsync(row,reservation,"NotCharged",Guid.NewGuid(),default);recovery.Record(row,reservation,from,Guid.NewGuid());
   await db.SaveChangesAsync();await tx.CommitAsync();
  }
  await flow.Release();Assert.Equal("Failed",(await flow.Saga()).State);
  Assert.True(await flow.Pay());await flow.Pass("payments","reservations","PaymentSucceeded");Assert.Equal("Compensating",(await flow.Saga()).State);
  await flow.Refund();await flow.Pass("payments","reservations","PaymentRefunded");Assert.Equal("Failed",(await flow.Saga()).State);
  await flow.Stock(6);await using var check=BookingSqlTests.Reservations();
  Assert.Equal(1,await check.Outbox.CountAsync(x=>x.EffectKey==$"saga/{saga.Id}/failed"));Assert.False(await check.Outbox.AnyAsync(x=>x.EffectKey==$"saga/{saga.Id}/confirm"));
 }
 [SqlFact]
 public async Task AdminCanRetryUnpaidAvailabilityReleaseWithoutPretendingAnUnrequestedPaymentWasResolved(){
  await using var flow=new Flow();await flow.Init();flow.Clock.Now=flow.Clock.Now.AddSeconds(31);await flow.Due();
  flow.Clock.Now=flow.Clock.Now.AddSeconds(121);await flow.Due();var saga=await flow.Saga();Assert.Equal("ManualReview",saga.State);Assert.False(saga.PaymentRequested);
  await using(var db=BookingSqlTests.Reservations()){
   var result=await new SagaCommands(db,new(db,flow.Clock,new()),flow.Clock).RetryAsync(saga.Id,"test-admin",default);
   Assert.False(result.PaymentRequested);Assert.False(result.PaymentResolved);Assert.Equal(saga.HoldId,result.HoldId);
   var retry=await db.Outbox.SingleAsync(x=>x.EffectKey.StartsWith($"saga/{saga.Id}/release-retry/"));
   await flow.Consume("catalog",MessageCodec.Parse(retry.EnvelopeJson));
  }
  await flow.Pass("catalog","reservations","SeatsReleased");Assert.Equal("Failed",(await flow.Saga()).State);await flow.Stock(6);
  await using var check=BookingSqlTests.Payments();Assert.False(await check.Operations.AnyAsync(x=>x.SagaId==saga.Id));
 }
}
