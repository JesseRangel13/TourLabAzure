using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TourLab.Azure.Infrastructure.Operations;
namespace TourLab.Azure.Tests;
[Collection("SQL Identity")]
public sealed class LiveRecoveryTests {
 private static async Task Set(HttpClient admin,string service,string mode,int count){
  var csrf=await admin.GetFromJsonAsync<JsonElement>("/auth/csrf");
  using var request=new HttpRequestMessage(HttpMethod.Put,$"/api/{service}/v1/admin/faults"){Content=JsonContent.Create(new{mode,occurrences=count})};
  request.Headers.Add("Origin","https://localhost:9443");request.Headers.Add("X-AZURELAB-XSRF-TOKEN",csrf.GetProperty("requestToken").GetString());
  using var response=await admin.SendAsync(request);Assert.Equal(HttpStatusCode.OK,response.StatusCode);
 }
 private static async Task Reset(HttpClient admin){
  foreach(var service in new[]{"catalog","reservations","payments","notifications"}){
   using var response=await LiveBookingTests.Post(admin,$"/api/{service}/v1/admin/faults/reset",new{});Assert.Equal(HttpStatusCode.OK,response.StatusCode);
  }
 }
 private static async Task<(Guid Id,Guid Tour,Guid Session,string TraceId)> Book(HttpClient admin,HttpClient alice,string? key=null){
  using var activity=TourLab.Azure.Infrastructure.LabTelemetry.Start("test.live-booking",System.Diagnostics.ActivityKind.Client,null,null);
  using var tourResponse=await LiveBookingTests.Post(admin,"/api/catalog/v1/admin/tours",new{name="Synthetic recovery "+Guid.NewGuid(),description="Actual broker fault and recovery evidence"});
  Assert.Equal(HttpStatusCode.Created,tourResponse.StatusCode);
  var tour=(await tourResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
  using var sessionResponse=await LiveBookingTests.Post(admin,$"/api/catalog/v1/admin/tours/{tour}/sessions",new{startsAtUtc=DateTimeOffset.UtcNow.AddDays(2),unitAmountMinor=12345,capacity=6});
  Assert.Equal(HttpStatusCode.Created,sessionResponse.StatusCode);
  var session=(await sessionResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
  using var response=await LiveBookingTests.Post(alice,"/api/reservations/v1/reservations",new{sessionId=session,participants=2,expectedUnitAmountMinor=12345,expectedCurrency="MXN"},key??Guid.NewGuid().ToString(),activity!.Id);
  Assert.Equal(HttpStatusCode.Accepted,response.StatusCode);
  return((await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("reservationId").GetGuid(),tour,session,activity!.TraceId.ToString());
 }
 private static async Task<JsonElement> Wait(HttpClient client,string path,Func<JsonElement,bool> condition){
  using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(60));
  while(true){var value=await client.GetFromJsonAsync<JsonElement>(path,timeout.Token);if(condition(value))return value;await Task.Delay(250,timeout.Token);}
 }
 [WorkflowFact]
 public async Task RealBrokerCancellationDeclineAndConfirmationFailureKeepOneFinancialEffectAndRestoreStock(){
  using var admin=LiveBookingTests.Client();using var alice=LiveBookingTests.Client();using var bob=LiveBookingTests.Client();
  await LiveBookingTests.Login(admin,"Admin");await LiveBookingTests.Login(alice,"Alice");await LiveBookingTests.Login(bob,"Bob");
  try{
   foreach(var mode in new[]{"Success","Decline","RejectNextConfirmation"}){
    await Reset(admin);
    await Set(admin,mode=="RejectNextConfirmation"?"catalog":"payments",mode,1);
    var booking=await Book(admin,alice);
    var path=$"/api/reservations/v1/reservations/{booking.Id}";
    if(mode=="Success"){
     await Wait(alice,path,x=>x.GetProperty("status").GetString()=="Confirmed");
     using(var hidden=await LiveBookingTests.Post(bob,path+"/cancel",new{},Guid.NewGuid().ToString()))Assert.Equal(HttpStatusCode.NotFound,hidden.StatusCode);
     var requests=await Task.WhenAll(LiveBookingTests.Post(alice,path+"/cancel",new{},Guid.NewGuid().ToString()),LiveBookingTests.Post(alice,path+"/cancel",new{},Guid.NewGuid().ToString()));
     foreach(var response in requests){using(response)Assert.Equal(HttpStatusCode.Accepted,response.StatusCode);}
    }
    var terminal=mode=="Success"?"Cancelled":"Failed";
    await Wait(alice,path,x=>x.GetProperty("status").GetString()==terminal);
    var payments=await alice.GetFromJsonAsync<JsonElement[]>($"/api/payments/v1/payments?reservationId={booking.Id}");
    var payment=Assert.Single(payments!);var operation=payment.GetProperty("id").GetGuid();
    await using(var db=BookingSqlTests.Payments()){
     Assert.Equal(1,await db.Effects.CountAsync(x=>x.Id==operation));
     Assert.Equal(mode=="Decline"?0:1,await db.ProviderRefunds.CountAsync(x=>x.PaymentOperationId==operation&&x.Reference!=null));
    }
    var sessions=await alice.GetFromJsonAsync<JsonElement[]>($"/api/catalog/v1/tours/{booking.Tour}/sessions");
    Assert.Equal(6,Assert.Single(sessions!).GetProperty("availableSeats").GetInt32());
   }
   foreach(var service in new[]{"catalog","reservations","payments","notifications"}){
    using var denied=await alice.GetAsync($"/api/{service}/v1/admin/diagnostics");Assert.Equal(HttpStatusCode.Forbidden,denied.StatusCode);
    var diagnostic=await admin.GetFromJsonAsync<JsonElement>($"/api/{service}/v1/admin/diagnostics?pageSize=2");
    Assert.Equal(service,diagnostic.GetProperty("service").GetString());
   }
  }finally{await Reset(admin);}
 }
 [WorkflowFact]
 public async Task UnknownProviderResolutionAndNotificationTransientFailuresRecoverThroughActualBroker(){
  using var admin=LiveBookingTests.Client();using var alice=LiveBookingTests.Client();
  await LiveBookingTests.Login(admin,"Admin");await LiveBookingTests.Login(alice,"Alice");
  try{
   await Reset(admin);await Set(admin,"payments","UnknownUntilAdminResolution",1);await Set(admin,"notifications","FailureThenSuccess",1);
   var booking=await Book(admin,alice);
   var payments=await Wait(alice,$"/api/payments/v1/payments?reservationId={booking.Id}",x=>x.GetArrayLength()==1&&x[0].GetProperty("status").GetString()=="Unknown");
   var operation=payments[0].GetProperty("id").GetGuid();
   using(var resolved=await LiveBookingTests.Post(admin,$"/api/payments/v1/admin/fake-provider/{operation}/resolve",new{outcome="Succeeded"}))Assert.Equal(HttpStatusCode.Accepted,resolved.StatusCode);
   using(var reconcile=await LiveBookingTests.Post(admin,$"/api/payments/v1/admin/payments/{operation}/reconcile",new{}))Assert.Equal(HttpStatusCode.Accepted,reconcile.StatusCode);
   await Wait(alice,$"/api/reservations/v1/reservations/{booking.Id}",x=>x.GetProperty("status").GetString()=="Confirmed");
   var notices=await Wait(alice,$"/api/notifications/v1/notifications?reservationId={booking.Id}",x=>x.GetProperty("items").GetArrayLength()==1&&x.GetProperty("items")[0].GetProperty("status").GetString()=="Sent");
   var id=notices.GetProperty("items")[0].GetProperty("id").GetGuid();
      await using(var db=BookingSqlTests.Payments()){
    Assert.Equal(1,await db.Effects.CountAsync(x=>x.Id==operation));
    var row=await db.Operations.SingleAsync(x=>x.Id==operation);
    Assert.True(System.Diagnostics.ActivityContext.TryParse(row.Traceparent,row.Tracestate,out var parent));Assert.Equal(booking.TraceId,parent.TraceId.ToString());
   }
      await using(var db=BookingSqlTests.Notifications()){
    Assert.Equal(2,(await db.Receipts.SingleAsync(x=>x.Id==id)).Failures);
    var row=await db.Notifications.SingleAsync(x=>x.Id==id);
    Assert.True(System.Diagnostics.ActivityContext.TryParse(row.Traceparent,row.Tracestate,out var parent));Assert.Equal(booking.TraceId,parent.TraceId.ToString());
   }
  }finally{await Reset(admin);}
 }
 [WorkflowFact]
 public async Task ActualQuoteTimeoutReturns503WithoutHiddenSagaAndReadinessProbesOwnedBroker(){
  using var admin=LiveBookingTests.Client();using var alice=LiveBookingTests.Client();
  await LiveBookingTests.Login(admin,"Admin");await LiveBookingTests.Login(alice,"Alice");
  try{
   await Reset(admin);
   foreach(var service in new[]{"catalog","reservations","payments","notifications"}){
    var ready=await alice.GetFromJsonAsync<JsonElement>($"/api/{service}/health/ready");
    Assert.Equal("Ready",ready.GetProperty("database").GetString());Assert.Equal("Ready",ready.GetProperty("messaging").GetString());
   }
   using var tourResponse=await LiveBookingTests.Post(admin,"/api/catalog/v1/admin/tours",new{name="Synthetic timeout "+Guid.NewGuid(),description="Bounded actual HTTPS fault"});
   var tour=(await tourResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
   using var sessionResponse=await LiveBookingTests.Post(admin,$"/api/catalog/v1/admin/tours/{tour}/sessions",new{startsAtUtc=DateTimeOffset.UtcNow.AddDays(2),unitAmountMinor=12345,capacity=6});
   var session=(await sessionResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
   int before;await using(var db=BookingSqlTests.Reservations())before=await db.Sagas.CountAsync();
   await Set(admin,"catalog","QuoteTimeout",3);
   using var response=await LiveBookingTests.Post(alice,"/api/reservations/v1/reservations",new{sessionId=session,participants=1,expectedUnitAmountMinor=12345,expectedCurrency="MXN"},Guid.NewGuid().ToString());
   Assert.Equal(HttpStatusCode.ServiceUnavailable,response.StatusCode);
   await using(var db=BookingSqlTests.Reservations())Assert.Equal(before,await db.Sagas.CountAsync());
   var faults=await admin.GetFromJsonAsync<JsonElement>("/api/catalog/v1/admin/faults");Assert.Equal(0,faults.GetProperty("remaining").GetInt32());
   var inspected=await admin.GetFromJsonAsync<DeadLetterView[]>("/api/catalog/v1/admin/dead-letters?source=commands&pageSize=10");
   Assert.NotNull(inspected);Assert.InRange(inspected.Length,1,10);Assert.Contains(inspected,x=>!x.Replayable);
  }finally{await Reset(admin);}
 }
}
