using System.Text.Json;
using Azure.Messaging.ServiceBus;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using TourLab.Azure.Infrastructure.Messaging;
using TourLab.Azure.Infrastructure.Persistence;
using TourLab.Azure.Reservations.Dashboard;
namespace TourLab.Azure.Tests;
public sealed class CosmosBrokerFactAttribute:FactAttribute
{
    public CosmosBrokerFactAttribute(){if(Environment.GetEnvironmentVariable("AZURELAB_COSMOS")!="1"||Environment.GetEnvironmentVariable("AZURELAB_LIVE_BROKER")!="1")Skip="NOT VERIFIED: use Verify-Dashboard.ps1 -IncludeHttps.";}
}
[Collection("SQL Identity")]
public sealed class BrokerDashboardTests
{
    private sealed class Development:IHostEnvironment
    {
        public string EnvironmentName{get;set;}=Environments.Development;
        public string ApplicationName{get;set;}="DashboardTest";
        public string ContentRootPath{get;set;}=TestSettings.Root;
        public IFileProvider ContentRootFileProvider{get;set;}=new NullFileProvider();
    }
    private static ServiceBusTransport Transport()
    {
        var c=new ConfigurationBuilder().AddJsonFile(Path.Combine(TestSettings.Output,"reservations.json")).Build();
        return new(Options.Create(c.GetSection("Messaging").Get<MessagingOptions>()!),new Development());
    }
    private static async Task<ServiceBusReceivedMessage> Own(ServiceBusReceiver receiver,string id,DashboardConsumer consumer,CancellationToken ct)
    {
        for(var i=0;i<50;i++)
        {
            var message=await receiver.ReceiveMessageAsync(TimeSpan.FromSeconds(3),ct);
            if(message is null)continue;
            if(message.MessageId==id)return message;
            // Apply legitimate retained lab traffic through the same consumer; never discard unrelated messages.
            await consumer.ConsumeAsync(MessageCodec.Parse(message.Body.ToString()),ct);
            await receiver.CompleteMessageAsync(message,ct);
        }
        throw new InvalidOperationException("Bounded dashboard receive exhausted.");
    }
    [CosmosBrokerFact]
    public async Task ActualOutboxPublishAndCosmosSuccessBeforeCompleteRedeliveryHaveOneVersionedEffectWithoutSqlInbox()
    {
        await using var transport=Transport();using var store=CosmosIntegrationTests.Store();var consumer=new DashboardConsumer(store);
        await using var receiver=transport.Client.CreateReceiver(MessageRoutes.Topic,"dashboard-events");
        var row=DashboardSqlTests.Row("broker-dashboard-"+Guid.NewGuid());
        Guid delivery;Guid messageId;
        await using(var db=BookingSqlTests.Reservations())
        {
            db.Reservations.Add(row);BookingSnapshots.Add(db,row,TimeProvider.System.GetUtcNow(),Guid.NewGuid());await db.SaveChangesAsync();
            var outbox=await db.Outbox.SingleAsync(x=>x.EffectKey=="booking/"+row.Id+"/1");
            Assert.Null(outbox.PublishedAtUtc);delivery=outbox.DeliveryId;messageId=outbox.MessageId;
        }
        var leases=new SqlOutboxLeases(TestSettings.Configuration.Runtime["reservations"],"reservations",TimeProvider.System);
        var owner=Guid.NewGuid();using var budget=new CancellationTokenSource(TimeSpan.FromSeconds(45));
        var claimed=Assert.Single(await leases.ClaimAsync(owner,1,TimeSpan.FromSeconds(60),MessageRoutes.Topic,budget.Token));
        Assert.Equal(delivery,claimed.DeliveryId);await transport.SendAsync(claimed,budget.Token);
        Assert.True(await leases.MarkPublishedAsync(delivery,owner,budget.Token));
        var first=await Own(receiver,messageId.ToString("D"),consumer,budget.Token);
        await consumer.ConsumeAsync(MessageCodec.Parse(first.Body.ToString()),budget.Token);
        // Cosmos is committed; process loses Complete. No SQL Inbox can suppress the retry.
        await receiver.AbandonMessageAsync(first,cancellationToken:budget.Token);
        var again=await Own(receiver,messageId.ToString("D"),consumer,budget.Token);
        Assert.True(again.DeliveryCount>first.DeliveryCount);
        await consumer.ConsumeAsync(MessageCodec.Parse(again.Body.ToString()),budget.Token);await receiver.CompleteMessageAsync(again,budget.Token);
        await transport.SendAsync(claimed,budget.Token);
        var duplicate=await Own(receiver,messageId.ToString("D"),consumer,budget.Token);
        await consumer.ConsumeAsync(MessageCodec.Parse(duplicate.Body.ToString()),budget.Token);await receiver.CompleteMessageAsync(duplicate,budget.Token);
        var page=await store.QueryAsync(row.UserId,10,null,budget.Token);Assert.Single(page.Items);Assert.Equal(1,page.Items[0].BusinessVersion);
        await using var verify=BookingSqlTests.Reservations();Assert.False(await verify.Inbox.AnyAsync(x=>x.MessageId==messageId));
    }
    [CosmosBrokerFact]
    public async Task FailedActualSdkWriteIsNotAcknowledgedAndRedeliveryRecoversThroughCosmos()
    {
        await using var transport=Transport();using var good=CosmosIntegrationTests.Store();var consumer=new DashboardConsumer(good);
        var s=CosmosIntegrationTests.Snapshot();var id=Guid.NewGuid();
        var body=MessageRoutes.Serialize(s,id,Guid.NewGuid(),DateTimeOffset.UtcNow,null,id,id);
        await using var sender=transport.Client.CreateSender(MessageRoutes.Topic);
        await using var receiver=transport.Client.CreateReceiver(MessageRoutes.Topic,"dashboard-events");
        var message=new ServiceBusMessage(body){MessageId=id.ToString("D"),Subject="BookingSnapshotChanged"};
        message.ApplicationProperties["type"]="BookingSnapshotChanged";await sender.SendMessageAsync(message);
        using var budget=new CancellationTokenSource(TimeSpan.FromSeconds(40));
        var first=await Own(receiver,id.ToString("D"),consumer,budget.Token);
        Assert.DoesNotContain(System.Net.NetworkInformation.IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpListeners(),e=>e.Port==18089);
        var options=CosmosIntegrationTests.Settings();options.Endpoint="https://localhost:18089/";
        using(var failed=new CosmosBookingProjection(Options.Create(options),TimeProvider.System))
        using(var outage=new CancellationTokenSource(TimeSpan.FromSeconds(3)))
        {
            var failure=await Record.ExceptionAsync(()=>new DashboardConsumer(failed).ConsumeAsync(MessageCodec.Parse(first.Body.ToString()),outage.Token));
            Assert.NotNull(failure);
            Assert.True(failure is Microsoft.Azure.Cosmos.CosmosException or OperationCanceledException or HttpRequestException,failure.GetType().Name);
        }
        Assert.Null(await good.ReadAsync(s.UserId,s.ReservationId,budget.Token));
        await using(var db=BookingSqlTests.Reservations())Assert.False(await db.Inbox.AnyAsync(x=>x.MessageId==id));
        await receiver.AbandonMessageAsync(first,cancellationToken:budget.Token);
        var retry=await Own(receiver,id.ToString("D"),consumer,budget.Token);Assert.True(retry.DeliveryCount>first.DeliveryCount);
        await consumer.ConsumeAsync(MessageCodec.Parse(retry.Body.ToString()),budget.Token);await receiver.CompleteMessageAsync(retry,budget.Token);
        Assert.NotNull(await good.ReadAsync(s.UserId,s.ReservationId,budget.Token));
    }
}
