using Microsoft.Extensions.Configuration;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using TourLab.Azure.Gateway.Identity;
namespace TourLab.Azure.Tests;
public sealed class WorkflowFactAttribute:FactAttribute
{
    public WorkflowFactAttribute(){if(Environment.GetEnvironmentVariable("AZURELAB_LIVE_WORKFLOW")!="1")Skip="NOT VERIFIED: actual Service Bus plus HTTPS hosts required.";}
}
[Collection("SQL Identity")]
public sealed class LiveBookingTests
{
    private static HttpClient Client()
    {
        var handler=new SocketsHttpHandler{UseCookies=true,AllowAutoRedirect=false,ConnectTimeout=TimeSpan.FromSeconds(3)};
        handler.SslOptions.CertificateChainPolicy=new X509ChainPolicy{TrustMode=X509ChainTrustMode.CustomRootTrust,RevocationMode=X509RevocationMode.NoCheck};
        handler.SslOptions.CertificateChainPolicy.CustomTrustStore.Add(X509CertificateLoader.LoadCertificateFromFile(Path.Combine(TestSettings.Root,".local","certificates","ca.crt")));
        return new(handler){BaseAddress=new Uri("https://localhost:9443"),Timeout=TimeSpan.FromSeconds(15)};
    }
    private static async Task<HttpResponseMessage> Post(HttpClient client,string path,object body,string? key=null)
    {
        var csrf=await client.GetFromJsonAsync<JsonElement>("/auth/csrf");
        using var request=new HttpRequestMessage(HttpMethod.Post,path){Content=JsonContent.Create(body)};
        request.Headers.Add("Origin","https://localhost:9443");
        request.Headers.Add("X-AZURELAB-XSRF-TOKEN",csrf.GetProperty("requestToken").GetString());
        if(key is not null)request.Headers.Add("Idempotency-Key",key);
        return await client.SendAsync(request);
    }
    private static async Task Login(HttpClient client,string name)
    {
        var seed=TestSettings.Configuration.Users.Single(x=>x.UserName==name);
        using var result=await Post(client,"/auth/login",new LoginRequest(seed.UserName,seed.Password));
        Assert.Equal(HttpStatusCode.OK,result.StatusCode);
    }
    [WorkflowFact]
    public async Task RealServiceBusBookingConfirmsOneChargeAndNotificationAndEnforcesAliceBobOwnership()
    {
        using var admin=Client();using var alice=Client();using var bob=Client();
        await Login(admin,"Admin");await Login(alice,"Alice");await Login(bob,"Bob");
        using var tourResponse=await Post(admin,"/api/catalog/v1/admin/tours",new{name="Live booking "+Guid.NewGuid(),description="Synthetic actual broker workflow"});
        Assert.Equal(HttpStatusCode.Created,tourResponse.StatusCode);
        var tour=(await tourResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        using var sessionResponse=await Post(admin,$"/api/catalog/v1/admin/tours/{tour}/sessions",new{startsAtUtc=DateTimeOffset.UtcNow.AddDays(3),unitAmountMinor=12345,capacity=6});
        Assert.Equal(HttpStatusCode.Created,sessionResponse.StatusCode);
        var session=(await sessionResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var key=Guid.NewGuid().ToString();
        var input=new{sessionId=session,participants=2,expectedUnitAmountMinor=12345,expectedCurrency="MXN"};
        using var created=await Post(alice,"/api/reservations/v1/reservations",input,key);
        Assert.Equal(HttpStatusCode.Accepted,created.StatusCode);
        var original=await created.Content.ReadAsStringAsync();
        var id=JsonDocument.Parse(original).RootElement.GetProperty("reservationId").GetGuid();
        var path=$"/api/reservations/v1/reservations/{id}";
        using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(60));
        JsonElement current;
        do {
            current=await alice.GetFromJsonAsync<JsonElement>(path,timeout.Token);
            var status=current.GetProperty("status").GetString();
            Assert.DoesNotContain(status,new[]{"Failed","ManualReview","Cancelled"});
            if(status=="Confirmed")break;
            await Task.Delay(250,timeout.Token);
        }while(true);
        Assert.Equal(24690,current.GetProperty("totalAmountMinor").GetInt64());
        using var repeat=await Post(alice,"/api/reservations/v1/reservations",input,key);
        Assert.Equal(HttpStatusCode.Accepted,repeat.StatusCode);Assert.Equal(original,await repeat.Content.ReadAsStringAsync());
        using var conflict=await Post(alice,"/api/reservations/v1/reservations",new{sessionId=session,participants=3,expectedUnitAmountMinor=12345,expectedCurrency="MXN"},key);
        Assert.Equal(HttpStatusCode.Conflict,conflict.StatusCode);
        using(var hidden=await bob.GetAsync(path))Assert.Equal(HttpStatusCode.NotFound,hidden.StatusCode);
        using(var hidden=await bob.GetAsync(path+"/timeline"))Assert.Equal(HttpStatusCode.NotFound,hidden.StatusCode);
        var timeline=await alice.GetFromJsonAsync<JsonElement[]>(path+"/timeline");
        Assert.Contains(timeline!,x=>x.GetProperty("to").GetString()=="Confirmed");
        var payments=await alice.GetFromJsonAsync<JsonElement[]>($"/api/payments/v1/payments?reservationId={id}");
        var payment=Assert.Single(payments!);
        Assert.True(payment.GetProperty("simulated").GetBoolean());Assert.Equal("Succeeded",payment.GetProperty("status").GetString());
        using(var hidden=await bob.GetAsync("/api/payments/v1/payments/"+payment.GetProperty("id").GetGuid()))Assert.Equal(HttpStatusCode.NotFound,hidden.StatusCode);
        JsonElement notification=default;
        do {
            var notifications=await alice.GetFromJsonAsync<JsonElement>($"/api/notifications/v1/notifications?reservationId={id}",timeout.Token);
            var items=notifications.GetProperty("items").EnumerateArray().ToArray();
            if(items.Length==1&&items[0].GetProperty("status").GetString()=="Sent"){notification=items[0];break;}
            await Task.Delay(250,timeout.Token);
        }while(true);
        Assert.True(notification.GetProperty("simulated").GetBoolean());Assert.StartsWith("SIMULATED",notification.GetProperty("subject").GetString());
        using(var hidden=await bob.GetAsync("/api/notifications/v1/notifications/"+notification.GetProperty("id").GetGuid()))Assert.Equal(HttpStatusCode.NotFound,hidden.StatusCode);
        var sessions=await alice.GetFromJsonAsync<JsonElement[]>($"/api/catalog/v1/tours/{tour}/sessions");
        Assert.Equal(4,Assert.Single(sessions!).GetProperty("availableSeats").GetInt32());
    }
    [WorkflowFact]
    public async Task MalformedCommandIsRetainedInTheBuiltInDlqWithoutAnInventoryEffect()
    {
        var config=new Microsoft.Extensions.Configuration.ConfigurationBuilder().AddJsonFile(Path.Combine(TestSettings.Output,"catalog.json")).Build();
        var connection=config["Messaging:ConnectionString"]!;
        await using var client=new global::Azure.Messaging.ServiceBus.ServiceBusClient(connection);
        await using var sender=client.CreateSender("tourlab-azure-catalog-commands");
        await using var dead=client.CreateReceiver("tourlab-azure-catalog-commands",new global::Azure.Messaging.ServiceBus.ServiceBusReceiverOptions{
            SubQueue=global::Azure.Messaging.ServiceBus.SubQueue.DeadLetter});
        var id=Guid.NewGuid().ToString();
        var malformed=new global::Azure.Messaging.ServiceBus.ServiceBusMessage("{invalid-json"){MessageId=id,Subject="HoldSeats"};
        malformed.ApplicationProperties["type"]="HoldSeats";
        int before;await using(var db=BookingSqlTests.Catalog())before=await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.CountAsync(db.Holds);
        using var budget=new CancellationTokenSource(TimeSpan.FromSeconds(25));
        await sender.SendMessageAsync(malformed,budget.Token);
        long sequence=0;
        global::Azure.Messaging.ServiceBus.ServiceBusReceivedMessage? found=null;
        while(found is null){
            var batch=await dead.PeekMessagesAsync(20,sequence,budget.Token);
            found=batch.SingleOrDefault(x=>x.MessageId==id);
            if(batch.Count>0)sequence=batch.Max(x=>x.SequenceNumber)+1;
            if(found is null)await Task.Delay(200,budget.Token);
        }
        Assert.Equal("InvalidJson",found.DeadLetterReason);
        await using(var db=BookingSqlTests.Catalog())Assert.Equal(before,await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.CountAsync(db.Holds));
        // Peek only: preserve the DLQ message as evidence; do not purge it to clean up a test.
    }
}
