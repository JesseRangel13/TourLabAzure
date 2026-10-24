using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Hosting;
using TourLab.Azure.Infrastructure.Operations;
namespace TourLab.Azure.Tests;
[Collection("SQL Identity")]
public sealed class RecoveryHttpTests {
 private static async Task Roles<T>(string schema,string service) where T:class{
  await using var factory=new AzureFactory<T>(schema);
  using var tourist=RecoverySqlTests.Client(factory,service,"Tourist");
  using var admin=RecoverySqlTests.Client(factory,service);
  foreach(var path in new[]{"diagnostics","faults","dead-letters"}){
   using var denied=await tourist.GetAsync("/v1/admin/"+path);Assert.Equal(HttpStatusCode.Forbidden,denied.StatusCode);
  }
  using(var denied=await tourist.PutAsJsonAsync("/v1/admin/faults",new{mode="None",occurrences=1}))Assert.Equal(HttpStatusCode.Forbidden,denied.StatusCode);
  using(var denied=await tourist.PostAsJsonAsync("/v1/admin/dead-letters/"+Guid.NewGuid()+"/replay",new{}))Assert.Equal(HttpStatusCode.Forbidden,denied.StatusCode);
  var diagnostic=await admin.GetFromJsonAsync<JsonElement>("/v1/admin/diagnostics?page=1&pageSize=2");
  Assert.Equal(schema,diagnostic.GetProperty("service").GetString());
  var text=diagnostic.GetRawText();Assert.DoesNotContain("EnvelopeJson",text,StringComparison.OrdinalIgnoreCase);Assert.DoesNotContain("ConnectionString",text,StringComparison.OrdinalIgnoreCase);
  using(var bad=await admin.GetAsync("/v1/admin/diagnostics?pageSize=51"))Assert.Equal(HttpStatusCode.BadRequest,bad.StatusCode);
  using(var bad=await admin.PutAsJsonAsync("/v1/admin/faults",new{mode="ArbitraryCloudOperation",occurrences=101}))Assert.Equal(HttpStatusCode.BadRequest,bad.StatusCode);
  using(var selected=await admin.PutAsJsonAsync("/v1/admin/faults",new{mode=schema=="payments"?"Decline":schema=="notifications"?"FailureThenSuccess":schema=="catalog"?"QuoteTimeout":"PauseOutbox",occurrences=2}))Assert.Equal(HttpStatusCode.OK,selected.StatusCode);
  using(var reset=await admin.PostAsJsonAsync("/v1/admin/faults/reset",new{}))Assert.Equal(HttpStatusCode.OK,reset.StatusCode);
  await using var disabled=factory.WithWebHostBuilder(b=>b.ConfigureAppConfiguration((_,c)=>c.AddInMemoryCollection(new Dictionary<string,string?>{["LabFeaturesEnabled"]="false"})));
  using var closed=RecoverySqlTests.Client(disabled,service);
  using var hidden=await closed.GetAsync("/v1/admin/faults");Assert.Equal(HttpStatusCode.NotFound,hidden.StatusCode);
 }
 [SqlFact]public Task CatalogAdminBoundary()=>Roles<TourLab.Azure.Catalog.Program>("catalog","Catalog");
 [SqlFact]public Task ReservationsAdminBoundary()=>Roles<TourLab.Azure.Reservations.Program>("reservations","Reservations");
 [SqlFact]public Task PaymentsAdminBoundary()=>Roles<TourLab.Azure.Payments.Program>("payments","Payments");
 [SqlFact]public Task NotificationsAdminBoundary()=>Roles<TourLab.Azure.Notifications.Program>("notifications","Notifications");
 [SqlFact]
 public async Task CancellationEnforcesOwnerBeforeDepartureAndStableReplayAndConflict(){
  await using var flow=new RecoverySqlTests.Flow();await flow.Init();await flow.Confirmed();
  await using var factory=new AzureFactory<TourLab.Azure.Reservations.Program>("reservations");
  using var alice=RecoverySqlTests.Client(factory,"Reservations","Tourist");using var bob=RecoverySqlTests.Client(factory,"Reservations","Tourist","other-owner");
  var path=$"/v1/reservations/{flow.Id}/cancel";var key=Guid.NewGuid().ToString();
  async Task<HttpResponseMessage> Cancel(HttpClient client,string value){
   var request=new HttpRequestMessage(HttpMethod.Post,path){Content=JsonContent.Create(new{})};request.Headers.Add("Idempotency-Key",value);
   using(request)return await client.SendAsync(request);
  }
  using(var hidden=await Cancel(bob,key))Assert.Equal(HttpStatusCode.NotFound,hidden.StatusCode);
  using(var missing=await alice.PostAsJsonAsync(path,new{}))Assert.Equal(HttpStatusCode.BadRequest,missing.StatusCode);
  string body;using(var accepted=await Cancel(alice,key)){Assert.Equal(HttpStatusCode.Accepted,accepted.StatusCode);body=await accepted.Content.ReadAsStringAsync();}
  using(var same=await Cancel(alice,key)){Assert.Equal(HttpStatusCode.Accepted,same.StatusCode);Assert.Equal(body,await same.Content.ReadAsStringAsync());}
  await using var past=new RecoverySqlTests.Flow();await past.Init();await past.Confirmed();past.Clock.Now=past.Clock.Now.AddDays(3);
  await using var db=BookingSqlTests.Reservations();var error=await Assert.ThrowsAsync<TourLab.Azure.Reservations.Application.ReservationProblem>(()=>
   new TourLab.Azure.Reservations.Application.SagaCommands(db,new(db,past.Clock,new()),past.Clock).CancelAsync(past.Id,"recovery-owner",false,Guid.NewGuid().ToString(),default));
  Assert.Equal(409,error.Status);
 }
}
