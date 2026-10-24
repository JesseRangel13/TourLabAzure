using Microsoft.EntityFrameworkCore;
using TourLab.Azure.Infrastructure.Persistence;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using TourLab.Azure.Infrastructure.Messaging;
namespace TourLab.Azure.Tests;
public sealed class BrokerFactAttribute:FactAttribute
{
    public BrokerFactAttribute(){if(Environment.GetEnvironmentVariable("AZURELAB_LIVE_BROKER")!="1")Skip="NOT VERIFIED: use Verify-Workflow.ps1 with actual emulator.";}
}
[Collection("SQL Identity")]
public sealed class BrokerIntegrationTests
{
    private sealed class Development:IHostEnvironment
    {
        public string EnvironmentName{get;set;}=Environments.Development;
        public string ApplicationName{get;set;}="TourLab.Azure.Tests";
        public string ContentRootPath{get;set;}=TestSettings.Root;
        public IFileProvider ContentRootFileProvider{get;set;}=new NullFileProvider();
    }
    [BrokerFact]
    public async Task ActualTopicFiltersDeliverOnlyIntendedTypesWithoutMatchAllSubscriptions()
    {
        var config=new ConfigurationBuilder().AddJsonFile(Path.Combine(TestSettings.Output,"catalog.json")).Build();
        var options=config.GetSection("Messaging").Get<MessagingOptions>()!;
        await using var transport=new ServiceBusTransport(Options.Create(options),new Development());
        await using var sender=transport.Client.CreateSender(MessageRoutes.Topic);
        await using var reservations=transport.Client.CreateReceiver(MessageRoutes.Topic,"reservations-events");
        await using var notifications=transport.Client.CreateReceiver(MessageRoutes.Topic,"notifications-events");
        await using var catalog=transport.Client.CreateReceiver(MessageRoutes.Topic,"catalog-events");
        await using var payments=transport.Client.CreateReceiver(MessageRoutes.Topic,"payments-events");
        await using var dashboard=transport.Client.CreateReceiver(MessageRoutes.Topic,"dashboard-events");
        var id=Guid.NewGuid().ToString();
        var message=new ServiceBusMessage("Synthetic routing probe"){MessageId=id,Subject="TourSessionChanged",TimeToLive=TimeSpan.FromMinutes(2)};
        message.ApplicationProperties["type"]="TourSessionChanged";
        using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await sender.SendMessageAsync(message,timeout.Token);
        var delivered=await reservations.ReceiveMessageAsync(TimeSpan.FromSeconds(8),timeout.Token);
        Assert.NotNull(delivered);Assert.Equal(id,delivered.MessageId);
        await reservations.CompleteMessageAsync(delivered,timeout.Token);
        Assert.Null(await notifications.ReceiveMessageAsync(TimeSpan.FromSeconds(1),timeout.Token));
        Assert.Null(await catalog.ReceiveMessageAsync(TimeSpan.FromSeconds(1),timeout.Token));
        Assert.Null(await payments.ReceiveMessageAsync(TimeSpan.FromSeconds(1),timeout.Token));
        Assert.Null(await dashboard.ReceiveMessageAsync(TimeSpan.FromSeconds(1),timeout.Token));
        var notification=new ServiceBusMessage("Synthetic routing probe"){MessageId=Guid.NewGuid().ToString(),Subject="ReservationConfirmed",TimeToLive=TimeSpan.FromMinutes(2)};
        notification.ApplicationProperties["type"]="ReservationConfirmed";
        await sender.SendMessageAsync(notification,timeout.Token);
        var notify=await notifications.ReceiveMessageAsync(TimeSpan.FromSeconds(8),timeout.Token);
        Assert.NotNull(notify);Assert.Equal(notification.MessageId,notify.MessageId);
        await notifications.CompleteMessageAsync(notify,timeout.Token);
        Assert.Null(await reservations.ReceiveMessageAsync(TimeSpan.FromSeconds(1),timeout.Token));
    }
    [BrokerFact]
    public async Task RealOutboxSendBeforeMarkCrashAndInboxCommitBeforeCompleteRedeliveryHaveOneEffect()
    {
        var config=new ConfigurationBuilder().AddJsonFile(Path.Combine(TestSettings.Output,"catalog.json")).Build();
        await using var transport=new ServiceBusTransport(Options.Create(config.GetSection("Messaging").Get<MessagingOptions>()!),new Development());
        var id=Guid.NewGuid();var session=Guid.NewGuid();var delivery=Guid.NewGuid();
        var body=MessageRoutes.Serialize(new TourLab.Azure.Contracts.TourSessionChanged(session,"Broker SQL redelivery probe",false,1,100,"MXN"),
            id,delivery,DateTimeOffset.UtcNow,null,id,id);
        await using var receiver=transport.Client.CreateReceiver(MessageRoutes.Topic,"reservations-events");
        using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var clock=new BookingSqlTests.Clock();
        var leases=new SqlOutboxLeases(TestSettings.Configuration.Runtime["catalog"],"catalog",clock);
        await using(var db=BookingSqlTests.Catalog()){
            db.Outbox.Add(new(){DeliveryId=delivery,MessageId=id,Destination=MessageRoutes.Topic,
                EffectKey="broker-crash/"+id,Type="TourSessionChanged",OccurredAtUtc=clock.Now.AddYears(-1).UtcDateTime,EnvelopeJson=body});
            await db.SaveChangesAsync(timeout.Token);
        }
        await Assert.ThrowsAsync<BookingSqlTests.Crash>(()=>new OutboxDispatcher(leases,transport).DispatchOneAsync(timeout.Token,_=>throw new BookingSqlTests.Crash()));
        Guid oldOwner;
        await using(var db=BookingSqlTests.Catalog()){
            var row=await db.Outbox.SingleAsync(x=>x.DeliveryId==delivery,timeout.Token);
            Assert.Null(row.PublishedAtUtc);oldOwner=row.LeaseOwner!.Value;
        }
        var first=await receiver.ReceiveMessageAsync(TimeSpan.FromSeconds(8),timeout.Token);
        Assert.NotNull(first);Assert.Equal(id.ToString("D"),first.MessageId);
        await using(var db=BookingSqlTests.Reservations())
            await new TourLab.Azure.Reservations.Messaging.ReservationConsumer(db,TimeProvider.System).ConsumeAsync(MessageCodec.Parse(first.Body.ToString()),timeout.Token);
        await receiver.AbandonMessageAsync(first,cancellationToken:timeout.Token); // commit succeeded; Complete was lost
        var again=await receiver.ReceiveMessageAsync(TimeSpan.FromSeconds(8),timeout.Token);
        Assert.NotNull(again);Assert.Equal(first.MessageId,again.MessageId);Assert.True(again.DeliveryCount>first.DeliveryCount);
        await using(var db=BookingSqlTests.Reservations())
            await new TourLab.Azure.Reservations.Messaging.ReservationConsumer(db,TimeProvider.System).ConsumeAsync(MessageCodec.Parse(again.Body.ToString()),timeout.Token);
        await receiver.CompleteMessageAsync(again,timeout.Token);
        clock.Now=clock.Now.AddSeconds(61);
        Assert.True(await new OutboxDispatcher(leases,transport).DispatchOneAsync(timeout.Token));
        Assert.False(await leases.MarkPublishedAsync(delivery,oldOwner,timeout.Token));
        var resent=await receiver.ReceiveMessageAsync(TimeSpan.FromSeconds(8),timeout.Token);
        Assert.NotNull(resent);Assert.Equal(first.MessageId,resent.MessageId);
        await using(var db=BookingSqlTests.Reservations())
            await new TourLab.Azure.Reservations.Messaging.ReservationConsumer(db,TimeProvider.System).ConsumeAsync(MessageCodec.Parse(resent.Body.ToString()),timeout.Token);
        await receiver.CompleteMessageAsync(resent,timeout.Token);
        await using(var db=BookingSqlTests.Catalog())
            Assert.NotNull((await db.Outbox.SingleAsync(x=>x.DeliveryId==delivery,timeout.Token)).PublishedAtUtc);
        await using(var db=BookingSqlTests.Reservations()){
            Assert.Equal(1,await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.CountAsync(db.Inbox.Where(x=>x.MessageId==id),timeout.Token));
            Assert.Equal(1,await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.CountAsync(db.Projections.Where(x=>x.SessionId==session),timeout.Token));
        }
    }
}
