using Microsoft.Extensions.Configuration;
using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using System.Text.Json;
using TourLab.Azure.Reservations.Dashboard;
namespace TourLab.Azure.Tests;
[Collection("SQL Identity")]
public sealed class DashboardHttpTests
{
    [CosmosFact]
    public async Task UserPartitionsProtectedCursorsAndAdminRebuildAreEnforcedByBackend()
    {
        var alice="http-alice-"+Guid.NewGuid();var bob="http-bob-"+Guid.NewGuid();
        using var store=CosmosIntegrationTests.Store();var initial=CosmosIntegrationTests.Snapshot(alice);
        for(var i=0;i<3;i++)await store.ApplyAsync(initial with{ReservationId=Guid.NewGuid(),DepartureUtc=initial.DepartureUtc.AddDays(i)},default);
        var bobSnapshot=initial with{UserId=bob,ReservationId=Guid.NewGuid()};await store.ApplyAsync(bobSnapshot,default);
        await using var factory=new AzureFactory<TourLab.Azure.Reservations.Program>("reservations");
        using var a=RecoverySqlTests.Client(factory,"Reservations","Tourist",alice);
        using var b=RecoverySqlTests.Client(factory,"Reservations","Tourist",bob);
        using var admin=RecoverySqlTests.Client(factory,"Reservations","Admin","dashboard-admin");
        using var anonymous=factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized,(await anonymous.GetAsync("/v1/dashboard/bookings")).StatusCode);
        var first=(await a.GetFromJsonAsync<DashboardPage>("/v1/dashboard/bookings?pageSize=2&userId="+bob))!;
        Assert.Equal(2,first.Items.Length);Assert.All(first.Items,x=>Assert.Equal(alice,x.UserId));Assert.NotNull(first.Cursor);
        Assert.DoesNotContain(alice,first.Cursor);Assert.DoesNotContain("departure",first.Cursor);
        var next="/v1/dashboard/bookings?pageSize=2&cursor="+Uri.EscapeDataString(first.Cursor);
        var second=(await a.GetFromJsonAsync<DashboardPage>(next))!;Assert.Single(second.Items);Assert.Null(second.Cursor);
        Assert.Equal(HttpStatusCode.BadRequest,(await b.GetAsync(next)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,(await a.GetAsync(next.Replace("pageSize=2","pageSize=3"))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,(await a.GetAsync("/v1/dashboard/bookings/"+bobSnapshot.ReservationId)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,(await admin.GetAsync("/v1/dashboard/bookings/"+bobSnapshot.ReservationId)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,(await a.PostAsJsonAsync("/v1/admin/dashboard/rebuild",new RebuildRequest())).StatusCode);
        var rebuilt=await admin.PostAsJsonAsync("/v1/admin/dashboard/rebuild",new RebuildRequest(1));
        Assert.Equal(HttpStatusCode.Accepted,rebuilt.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,(await a.GetAsync("/v1/dashboard/bookings?pageSize=51")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,(await a.GetAsync("/v1/dashboard/bookings?pageSize=21")).StatusCode);
    }
    [SqlFact]
    public async Task DisabledDashboardIsExplicitlyUnavailableWithoutSqlFallback()
    {
        await using var factory=new AzureFactory<TourLab.Azure.Reservations.Program>("reservations");
        await using var configured=factory.WithWebHostBuilder(b=>b.ConfigureAppConfiguration((_,c)=>c.AddInMemoryCollection(
            new Dictionary<string,string?>{{"Dashboard:Enabled","false"}})));
        using var client=RecoverySqlTests.Client(configured,"Reservations");
        using var response=await client.GetAsync("/v1/dashboard/bookings");
        Assert.Equal(HttpStatusCode.ServiceUnavailable,response.StatusCode);
        var body=await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("DashboardDisabled",body.GetProperty("code").GetString());Assert.False(string.IsNullOrEmpty(body.GetProperty("traceId").GetString()));
        Assert.False(body.TryGetProperty("items",out _));
    }
    [CosmosFact]
    public async Task ActualSdkConnectionOutageDoesNotBlockSqlBookingAndReturnsSafe503()
    {
        Assert.DoesNotContain(System.Net.NetworkInformation.IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpListeners(),e=>e.Port==18089);
        var settings=CosmosIntegrationTests.Settings();settings.Endpoint="https://localhost:18089/";
        await using var factory=new AzureFactory<TourLab.Azure.Reservations.Program>("reservations");
        await using var configured=factory.WithWebHostBuilder(b=>b.ConfigureTestServices(s=>{
            s.RemoveAll<IBookingProjection>();s.AddSingleton<IBookingProjection>(_=>new CosmosBookingProjection(Options.Create(settings),TimeProvider.System));
        }));
        using var client=RecoverySqlTests.Client(configured,"Reservations");
        var unavailable=client.GetAsync("/v1/dashboard/bookings");
        // SQL workflow continues while the actual Cosmos SDK encounters a refused local connection.
        await using var flow=new RecoverySqlTests.Flow();await flow.Init();await flow.Confirmed();Assert.Equal("Confirmed",(await flow.Saga()).State);
        await flow.Cancel("dashboard-outage-"+Guid.NewGuid());await flow.Refund();await flow.Pass("payments","reservations","PaymentRefunded");await flow.Release();
        Assert.Equal("Cancelled",(await flow.Saga()).State);await flow.Stock(6);
        using var response=await unavailable;Assert.Equal(HttpStatusCode.ServiceUnavailable,response.StatusCode);
        var text=await response.Content.ReadAsStringAsync();Assert.DoesNotContain(settings.Key,text);Assert.DoesNotContain("items",text);
    }
}
