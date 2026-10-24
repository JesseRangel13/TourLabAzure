using Microsoft.Extensions.Configuration;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using TourLab.Azure.Reservations.Dashboard;
namespace TourLab.Azure.Tests;
public sealed class LiveDashboardFactAttribute:FactAttribute
{
    public LiveDashboardFactAttribute(){if(Environment.GetEnvironmentVariable("AZURELAB_COSMOS")!="1"||Environment.GetEnvironmentVariable("AZURELAB_LIVE_BFF")!="1")Skip="NOT VERIFIED: run Verify-Dashboard.ps1 -IncludeHttps.";}
}
[Collection("SQL Identity")]
public sealed class LiveDashboardTests
{
    private sealed record RestartState(string Cookie,string Cursor,int Size,Guid Reservation);
    private static async Task<HttpResponseMessage> Post(HttpClient client,string path,object value,string? key=null)=>
        await LiveBookingTests.Post(client,path,value,key);
    private static async Task Wait(HttpClient client,string path,Func<JsonElement,bool> condition,CancellationToken ct)
    {
        while(true)
        {
            using var response=await client.GetAsync(path,ct);
            if(response.StatusCode==HttpStatusCode.OK&&condition(await response.Content.ReadFromJsonAsync<JsonElement>(ct)))return;
            await Task.Delay(200,ct);
        }
    }
    [LiveDashboardFact]
    public async Task RoundTripRealBookingCancellationProjectionOwnershipRebuildAndPoisonUseFilteredSubscription()
    {
        var cookies=new CookieContainer();using var alice=LiveBffTests.Client(captureCookies:cookies);
        using var admin=LiveBffTests.Client();using var bob=LiveBffTests.Client();
        await LiveBffTests.Login(admin,"Admin");await LiveBffTests.Login(alice,"Alice");await LiveBffTests.Login(bob,"Bob");
        var name="Cosmos live snapshot "+Guid.NewGuid();
        using var created=await Post(admin,"/api/catalog/v1/admin/tours",new{name,description="Actual SQL/broker/Cosmos evidence"});
        Assert.Equal(HttpStatusCode.Created,created.StatusCode);var tour=(await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        using var sessionResponse=await Post(admin,$"/api/catalog/v1/admin/tours/{tour}/sessions",new{startsAtUtc=DateTimeOffset.UtcNow.AddDays(2),unitAmountMinor=12345,capacity=6});
        var session=(await sessionResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var ids=new List<Guid>();using var budget=new CancellationTokenSource(TimeSpan.FromSeconds(100));
        for(var i=0;i<3;i++)
        {
            using var booked=await Post(alice,"/api/reservations/v1/reservations",new{sessionId=session,participants=2,expectedUnitAmountMinor=12345,expectedCurrency="MXN"},Guid.NewGuid().ToString());
            Assert.Equal(HttpStatusCode.Accepted,booked.StatusCode);ids.Add((await booked.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("reservationId").GetGuid());
        }
        foreach(var id in ids)
        {
            await Wait(alice,"/api/reservations/v1/reservations/"+id,v=>v.GetProperty("status").GetString()=="Confirmed",budget.Token);
            await Wait(alice,"/api/reservations/v1/dashboard/bookings/"+id,v=>v.GetProperty("status").GetString()=="Confirmed",budget.Token);
        }
        var first=(await alice.GetFromJsonAsync<DashboardPage>("/api/reservations/v1/dashboard/bookings?pageSize=1",budget.Token))!;
        Assert.Single(first.Items);Assert.NotNull(first.Cursor);Assert.Equal(name,first.Items[0].TourNameSnapshot);Assert.Equal(24690,first.Items[0].AmountCents);
        Assert.True(first.EventuallyConsistent);
        Assert.Equal(HttpStatusCode.NotFound,(await bob.GetAsync("/api/reservations/v1/dashboard/bookings/"+ids[0],budget.Token)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,(await bob.GetAsync("/api/reservations/v1/dashboard/bookings?pageSize=1&cursor="+Uri.EscapeDataString(first.Cursor),budget.Token)).StatusCode);
        using var cancelled=await Post(alice,"/api/reservations/v1/reservations/"+ids[0]+"/cancel",new{},Guid.NewGuid().ToString());
        Assert.Equal(HttpStatusCode.Accepted,cancelled.StatusCode);
        await Wait(alice,"/api/reservations/v1/reservations/"+ids[0],v=>v.GetProperty("status").GetString()=="Cancelled",budget.Token);
        await Wait(alice,"/api/reservations/v1/dashboard/bookings/"+ids[0],v=>v.GetProperty("status").GetString()=="Cancelled",budget.Token);
        Assert.Equal(HttpStatusCode.Forbidden,(await Post(alice,"/api/reservations/v1/admin/dashboard/rebuild",new RebuildRequest(1))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,(await admin.PostAsJsonAsync("/api/reservations/v1/admin/dashboard/rebuild",new RebuildRequest(1))).StatusCode);
        using var rebuilt=await Post(admin,"/api/reservations/v1/admin/dashboard/rebuild",new RebuildRequest(1));
        Assert.Equal(HttpStatusCode.Accepted,rebuilt.StatusCode);var result=(await rebuilt.Content.ReadFromJsonAsync<RebuildResult>())!;
        Assert.Equal(1,result.Enqueued);
        var json=new RestartState(cookies.GetCookies(new Uri("https://localhost:9443"))["__Host-tourlab-azure"]!.Value,first.Cursor,1,ids[0]);
        await File.WriteAllTextAsync(Path.Combine(TestSettings.Output,"dashboard-restart.json"),JsonSerializer.Serialize(json));

        // Poison goes through the actual running dashboard processor, retained in its built-in DLQ.
        var settings=new Microsoft.Extensions.Configuration.ConfigurationBuilder().AddJsonFile(Path.Combine(TestSettings.Output,"reservations.json")).Build();
        await using var bus=new global::Azure.Messaging.ServiceBus.ServiceBusClient(settings["Messaging:ConnectionString"]);
        await using var sender=bus.CreateSender(TourLab.Azure.Infrastructure.Messaging.MessageRoutes.Topic);
        var messageId=Guid.NewGuid().ToString();
        var malformed=new global::Azure.Messaging.ServiceBus.ServiceBusMessage("{invalid-json"){MessageId=messageId,Subject="BookingSnapshotChanged"};
        malformed.ApplicationProperties["type"]="BookingSnapshotChanged";await sender.SendMessageAsync(malformed,budget.Token);
        await using var dlq=bus.CreateReceiver(TourLab.Azure.Infrastructure.Messaging.MessageRoutes.Topic,"dashboard-events",
            new global::Azure.Messaging.ServiceBus.ServiceBusReceiverOptions{SubQueue=global::Azure.Messaging.ServiceBus.SubQueue.DeadLetter});
        long sequence=0;
        while(true)
        {
            var batch=await dlq.PeekMessagesAsync(20,sequence,budget.Token);
            var poison=batch.SingleOrDefault(x=>x.MessageId==messageId);
            if(poison is not null){Assert.Equal("InvalidJson",poison.DeadLetterReason);break;}
            if(batch.Count>0)sequence=batch.Max(x=>x.SequenceNumber)+1;
            await Task.Delay(200,budget.Token);
        }
    }
    [LiveDashboardFact]
    public async Task RestartedReservationsAcceptsExistingProtectedCursorAndKeepsProjection()
    {
        var state=JsonSerializer.Deserialize<RestartState>(await File.ReadAllTextAsync(Path.Combine(TestSettings.Output,"dashboard-restart.json")))!;
        using var alice=LiveBffTests.Client();alice.DefaultRequestHeaders.Add("Cookie","__Host-tourlab-azure="+state.Cookie);
        using var next=await alice.GetAsync("/api/reservations/v1/dashboard/bookings?pageSize="+state.Size+"&cursor="+Uri.EscapeDataString(state.Cursor));
        Assert.Equal(HttpStatusCode.OK,next.StatusCode);Assert.Single((await next.Content.ReadFromJsonAsync<DashboardPage>())!.Items);
        var detail=await alice.GetFromJsonAsync<BookingDocument>("/api/reservations/v1/dashboard/bookings/"+state.Reservation);
        Assert.Equal("Cancelled",detail!.Status);
        var keyFiles=Directory.GetFiles(CosmosIntegrationTests.Settings().KeyDirectory,"key-*.xml");Assert.NotEmpty(keyFiles);
        Assert.All(keyFiles,file=>Assert.Contains("encryptedSecret",File.ReadAllText(file)));
    }
}
