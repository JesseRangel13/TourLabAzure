using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using TourLab.Azure.Contracts;
using TourLab.Azure.Infrastructure.Messaging;
using TourLab.Azure.Infrastructure.Persistence;
using TourLab.Azure.Payments.Application;
using TourLab.Azure.Notifications.Application;
using CApp = TourLab.Azure.Catalog.Application;
using CDb = TourLab.Azure.Catalog.Persistence.CatalogDb;
using RApp = TourLab.Azure.Reservations.Application;
using RDb = TourLab.Azure.Reservations.Persistence.ReservationsDb;
using PDb = TourLab.Azure.Payments.Persistence.PaymentsDb;
using NDb = TourLab.Azure.Notifications.Persistence.NotificationsDb;
using TourLab.Azure.Provisioning;
namespace TourLab.Azure.Tests;

[Collection("SQL Identity")]
public sealed class BookingSqlTests
{
    internal static CDb Catalog() => new(DatabaseProvisioner.Options<CDb>(TestSettings.Configuration.Runtime["catalog"],"catalog"));
    internal static RDb Reservations() => new(DatabaseProvisioner.Options<RDb>(TestSettings.Configuration.Runtime["reservations"],"reservations"));
    internal static PDb Payments() => new(DatabaseProvisioner.Options<PDb>(TestSettings.Configuration.Runtime["payments"],"payments"));
    internal static NDb Notifications() => new(DatabaseProvisioner.Options<NDb>(TestSettings.Configuration.Runtime["notifications"],"notifications"));
    internal sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now=DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow()=>Now;
    }
    internal sealed class Factory<T>(Func<T> create) : IDbContextFactory<T> where T:DbContext
    {
        public T CreateDbContext()=>create();
        public Task<T> CreateDbContextAsync(CancellationToken ct=default) {ct.ThrowIfCancellationRequested();return Task.FromResult(create());}
    }
    internal sealed class Crash : Exception;
    internal static IntegrationEnvelope<JsonElement> Envelope<T>(T payload,Guid saga,Guid? message=null) =>
        MessageCodec.Parse(MessageRoutes.Serialize(payload,message??Guid.NewGuid(),Guid.NewGuid(),DateTimeOffset.UtcNow,saga,saga,saga));
    internal sealed class QuoteFixture : IAsyncDisposable
    {
        private readonly AzureFactory<TourLab.Azure.Catalog.Program> host=new("catalog");
        private readonly HttpClient http;
        private readonly TourLab.Azure.Reservations.QuoteServiceTokens tokens;
        private readonly RApp.QuoteConcurrencyLimit limit=new(TimeProvider.System);
        public RApp.ICatalogQuoteClient Client {get;}
        public QuoteFixture()
        {
            http=host.CreateClient();
            var config=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{
                ["Quote:PrivateKeyPath"]=Path.Combine(TestSettings.Output,"quote-signing.key")}).Build();
            tokens=new(config,TimeProvider.System);
            Client=new RApp.CatalogQuoteClient(http,tokens,limit,TimeProvider.System,Options.Create(new RApp.CatalogClientOptions()));
        }
        public async ValueTask DisposeAsync(){http.Dispose();tokens.Dispose();limit.Dispose();await host.DisposeAsync();}
    }
    internal static async Task<CApp.SessionView> Session(int seats=6)
    {
        await using var db=Catalog();var commands=new CApp.CatalogCommands(db,TimeProvider.System);
        var tour=await commands.CreateAsync(new("Booking "+Guid.NewGuid(),"Synthetic workflow evidence"),default);
        return await commands.CreateSessionAsync(tour.Id,new(DateTimeOffset.UtcNow.AddDays(2),12345,seats),default);
    }
    private static async Task<T> ReadPayload<T>(DbContext db,string schema,string type,Guid saga)
    {
        var sql=$"SELECT [EnvelopeJson] AS [Value] FROM [{SqlRules.Schema(schema)}].[Outbox] WHERE [Type]={{0}}";
        var json=await db.Database.SqlQueryRaw<string>(sql,
            type).ToListAsync();
        return json.Select(MessageCodec.Parse).Where(x=>x.SagaId==saga).Select(MessageCodec.Payload<T>).Single();
    }
    [SqlFact]
    public async Task ConcurrentSameKeyCreatesOneReservationSagaHistoryAndHoldIntentAndChangedBodyConflicts()
    {
        var session=await Session();await using var quote=new QuoteFixture();var key=Guid.NewGuid().ToString();var user="same-key-"+Guid.NewGuid();
        var command=new RApp.CreateReservation(session.Id,2,12345,"MXN");
        async Task<TourLab.Azure.Reservations.Persistence.IdempotencyRequest> Create(){
            await using var db=Reservations();return await new RApp.ReservationCreator(db,quote.Client,TimeProvider.System).CreateAsync(user,key,command,default);
        }
        var pair=await Task.WhenAll(Create(),Create());
        Assert.Equal(pair[0].ResourceId,pair[1].ResourceId);Assert.Equal(pair[0].ResponseBody,pair[1].ResponseBody);
        await using var verify=Reservations();
        var saga=await verify.Sagas.SingleAsync(x=>x.ReservationId==pair[0].ResourceId);
        Assert.Equal(1,await verify.Reservations.CountAsync(x=>x.UserId==user));
        Assert.Equal(1,await verify.History.CountAsync(x=>x.SagaId==saga.Id));
        Assert.Equal(1,await verify.Outbox.CountAsync(x=>x.EffectKey==$"saga/{saga.Id}/hold"));
        Assert.Equal("tourlab-azure-catalog-commands",(await verify.Outbox.SingleAsync(x=>x.EffectKey==$"saga/{saga.Id}/hold")).Destination);
        var problem=await Assert.ThrowsAsync<RApp.ReservationProblem>(()=>new RApp.ReservationCreator(verify,quote.Client,TimeProvider.System)
            .CreateAsync(user,key,command with {Participants=3},default));
        Assert.Equal("IdempotencyConflict",problem.Code);
    }
    private sealed class UnavailableQuote : RApp.ICatalogQuoteClient
    {
        public Task<RApp.QuoteView> CreateAsync(RApp.CreateQuote command,CancellationToken ct)=>throw new RApp.ReservationProblem(503,"CatalogUnavailable");
    }
    [SqlFact]
    public async Task Quote503PersistsNoReservationSagaIdempotencyHistoryOrPayment()
    {
        var user="unavailable-"+Guid.NewGuid();await using var db=Reservations();
        var before=await db.Sagas.CountAsync();var history=await db.History.CountAsync();var outbox=await db.Outbox.CountAsync();
        var error=await Assert.ThrowsAsync<RApp.ReservationProblem>(()=>new RApp.ReservationCreator(db,new UnavailableQuote(),TimeProvider.System)
            .CreateAsync(user,Guid.NewGuid().ToString(),new(Guid.NewGuid(),1,100,"MXN"),default));
        Assert.Equal(503,error.Status);Assert.False(await db.Reservations.AnyAsync(x=>x.UserId==user));
        Assert.Equal(before,await db.Sagas.CountAsync());Assert.Equal(history,await db.History.CountAsync());Assert.Equal(outbox,await db.Outbox.CountAsync());
        Assert.False(await db.Idempotency.AnyAsync(x=>x.UserId==user));
        await using var payments=Payments();Assert.False(await payments.Operations.AnyAsync(x=>x.UserId==user));
    }
    [SqlFact]
    public async Task SqlConsumerContractSequenceConfirmsAndDurablySimulatesOneChargeAndNotification()
    {
        // Explicit SQL/handler contract test. This does not establish actual broker routing/settlement.
        var session=await Session();var user="flow-"+Guid.NewGuid();await using var quote=new QuoteFixture();
        Guid sagaId;Guid reservationId;
        await using(var db=Reservations()){
            var created=await new RApp.ReservationCreator(db,quote.Client,TimeProvider.System).CreateAsync(user,Guid.NewGuid().ToString(),new(session.Id,2,12345,"MXN"),default);
            reservationId=created.ResourceId;sagaId=(await db.Sagas.SingleAsync(x=>x.ReservationId==reservationId)).Id;
        }
        HoldSeats hold;await using(var db=Reservations())hold=await ReadPayload<HoldSeats>(db,"reservations","HoldSeats",sagaId);
        await using(var db=Catalog())await new TourLab.Azure.Catalog.Messaging.CatalogConsumer(db,new CApp.InventoryHandlers(db,TimeProvider.System)).ConsumeAsync(Envelope(hold,sagaId),default);
        SeatsHeld held;await using(var db=Catalog())held=await ReadPayload<SeatsHeld>(db,"catalog","SeatsHeld",sagaId);
        await using(var db=Reservations())await new TourLab.Azure.Reservations.Messaging.ReservationConsumer(db,TimeProvider.System).ConsumeAsync(Envelope(held,sagaId),default);
        ProcessPayment payment;await using(var db=Reservations())payment=await ReadPayload<ProcessPayment>(db,"reservations","ProcessPayment",sagaId);
        var accepted=Envelope(payment,sagaId);
        await using(var db=Payments())await new PaymentConsumer(db).ConsumeAsync(accepted,default);
        await using(var db=Payments())await new PaymentConsumer(db).ConsumeAsync(accepted with { DeliveryId=Guid.NewGuid()},default);
        await using(var db=Payments())Assert.True(await new PaymentProcessor(db,new DurableFakeProvider(new Factory<PDb>(Payments),TimeProvider.System),TimeProvider.System).ProcessAsync(default,payment.PaymentOperationId));
        PaymentSucceeded paid;await using(var db=Payments()){
            paid=await ReadPayload<PaymentSucceeded>(db,"payments","PaymentSucceeded",sagaId);
            Assert.Equal(1,await db.Effects.CountAsync(x=>x.Id==payment.PaymentOperationId));
        }
        await using(var db=Reservations())await new TourLab.Azure.Reservations.Messaging.ReservationConsumer(db,TimeProvider.System).ConsumeAsync(Envelope(paid,sagaId),default);
        ConfirmSeats confirm;await using(var db=Reservations())confirm=await ReadPayload<ConfirmSeats>(db,"reservations","ConfirmSeats",sagaId);
        await using(var db=Catalog())await new TourLab.Azure.Catalog.Messaging.CatalogConsumer(db,new CApp.InventoryHandlers(db,TimeProvider.System)).ConsumeAsync(Envelope(confirm,sagaId),default);
        SeatsConfirmed confirmed;await using(var db=Catalog())confirmed=await ReadPayload<SeatsConfirmed>(db,"catalog","SeatsConfirmed",sagaId);
        await using(var db=Reservations())await new TourLab.Azure.Reservations.Messaging.ReservationConsumer(db,TimeProvider.System).ConsumeAsync(Envelope(confirmed,sagaId),default);
        ReservationConfirmed notification;await using(var db=Reservations()){
            notification=await ReadPayload<ReservationConfirmed>(db,"reservations","ReservationConfirmed",sagaId);
            Assert.Equal("Confirmed",(await db.Reservations.SingleAsync(x=>x.Id==reservationId)).Status);
            Assert.Equal(4,await db.History.CountAsync(x=>x.SagaId==sagaId));
            Assert.Equal(404,(await Assert.ThrowsAsync<RApp.ReservationProblem>(()=>new RApp.ReservationQueries(db).DetailAsync(reservationId,"Bob",false,default))).Status);
        }
        var notify=Envelope(notification,sagaId);
        await using(var db=Notifications())await new NotificationConsumer(db,TimeProvider.System).ConsumeAsync(notify,default);
        await using(var db=Notifications())await new NotificationConsumer(db,TimeProvider.System).ConsumeAsync(notify with{MessageId=Guid.NewGuid()},default);
        Guid notificationId;await using(var db=Notifications())notificationId=(await db.Notifications.SingleAsync(x=>x.ReservationId==reservationId)).Id;
        await using(var db=Notifications())Assert.True(await new NotificationProcessor(db,new DurableFakeSender(new Factory<NDb>(Notifications),TimeProvider.System),TimeProvider.System).ProcessAsync(default,notificationId));
        await using(var db=Notifications()){Assert.Equal("Sent",(await db.Notifications.SingleAsync(x=>x.Id==notificationId)).Status);Assert.Equal(1,await db.Receipts.CountAsync(x=>x.Id==notificationId));}
        await using(var db=Catalog())Assert.Equal(4,(await db.Sessions.SingleAsync(x=>x.Id==session.Id)).AvailableSeats);
    }
    [SqlFact]
    public async Task ProviderCommitThenApplicationCrashRecoversAfterLeaseWithOneCharge()
    {
        var clock=new Clock();var id=Guid.NewGuid();var saga=Guid.NewGuid();
        await using(var db=Payments())await new PaymentConsumer(db).ConsumeAsync(Envelope(new ProcessPayment(id,Guid.NewGuid(),"crash-user",500,"MXN"),saga),default);
        var provider=new DurableFakeProvider(new Factory<PDb>(Payments),clock);
        await using(var db=Payments())await Assert.ThrowsAsync<Crash>(()=>new PaymentProcessor(db,provider,clock).ProcessAsync(default,id,_=>throw new Crash()));
        await using(var db=Payments()){Assert.Equal("Pending",(await db.Operations.SingleAsync(x=>x.Id==id)).Status);Assert.Equal(1,await db.Effects.CountAsync(x=>x.Id==id));}
        clock.Now=clock.Now.AddSeconds(61);
        await using(var db=Payments())Assert.True(await new PaymentProcessor(db,provider,clock).ProcessAsync(default,id));
        await using(var db=Payments()){Assert.Equal("Succeeded",(await db.Operations.SingleAsync(x=>x.Id==id)).Status);Assert.Equal(1,await db.Effects.CountAsync(x=>x.Id==id));}
    }
    private sealed class RecordingTransport : IMessageTransport
    {
        public readonly List<ClaimedDelivery> Sent=[];
        public Task SendAsync(ClaimedDelivery delivery,CancellationToken ct){ct.ThrowIfCancellationRequested();Sent.Add(delivery);return Task.CompletedTask;}
    }
    [SqlFact]
    public async Task OutboxSendBeforeMarkCrashResendsStableLogicalMessageAndCannotPublishUncommittedIntent()
    {
        var clock=new Clock();var id=Guid.NewGuid();var message=Guid.NewGuid();
        var occurred=clock.Now.AddYears(-1);
        var leases=new SqlOutboxLeases(TestSettings.Configuration.Runtime["catalog"],"catalog",clock);
        await using(var db=Catalog()){
            await using var transaction=await db.Database.BeginTransactionAsync();
            db.Outbox.Add(new(){DeliveryId=id,MessageId=message,Destination=MessageRoutes.Topic,EffectKey="crash/"+id,Type="TourSessionChanged",OccurredAtUtc=occurred.UtcDateTime,
                EnvelopeJson=MessageRoutes.Serialize(new TourSessionChanged(Guid.NewGuid(),"Synthetic",true,1,100,"MXN"),message,id,occurred,null,message,message)});
            await db.SaveChangesAsync();
            Assert.DoesNotContain(await leases.ClaimAsync(Guid.NewGuid(),1,TimeSpan.FromSeconds(60),MessageRoutes.Topic,default),x=>x.DeliveryId==id);
            await transaction.CommitAsync();
        }
        var transport=new RecordingTransport();
        await Assert.ThrowsAsync<Crash>(()=>new OutboxDispatcher(leases,transport).DispatchOneAsync(default,_=>throw new Crash()));
        Guid oldOwner;
        await using(var db=Catalog()){
            var row=await db.Outbox.SingleAsync(x=>x.DeliveryId==id);Assert.Null(row.PublishedAtUtc);oldOwner=row.LeaseOwner!.Value;
        }
        clock.Now=clock.Now.AddSeconds(61);
        Assert.True(await new OutboxDispatcher(leases,transport).DispatchOneAsync(default));
        Assert.Equal(2,transport.Sent.Count);Assert.All(transport.Sent,x=>Assert.Equal(message,x.MessageId));
        Assert.False(await leases.MarkPublishedAsync(id,oldOwner,default));
        await using(var db=Catalog())Assert.NotNull((await db.Outbox.SingleAsync(x=>x.DeliveryId==id)).PublishedAtUtc);
    }

    [SqlFact]
    public async Task InboxRollbackLeavesNoEffectAndCommitBeforeCompleteDuplicateIsNoOpButContradictionIsPoison()
    {
        var envelope=Envelope(new ProcessPayment(Guid.NewGuid(),Guid.NewGuid(),"inbox-crash",100,"MXN"),Guid.NewGuid());
        await using(var db=Payments()){
            await Assert.ThrowsAsync<Crash>(()=>InboxTransaction.ProcessAsync(db,"payments","PaymentsAcceptance",envelope,async ct=>{
                db.Operations.Add(new(){Id=envelope.Payload.GetProperty("paymentOperationId").GetGuid(),SagaId=envelope.SagaId!.Value,
                    ReservationId=Guid.NewGuid(),UserId="inbox-crash",AmountMinor=100,Currency="MXN"});
                await db.SaveChangesAsync(ct);throw new Crash();
            },default));
        }
        await using(var db=Payments()){
            Assert.False(await db.Inbox.AnyAsync(x=>x.MessageId==envelope.MessageId));
            Assert.False(await db.Operations.AnyAsync(x=>x.Id==envelope.Payload.GetProperty("paymentOperationId").GetGuid()));
            await new PaymentConsumer(db).ConsumeAsync(envelope,default);
        }
        // Simulate a lost Complete: same logical message is handed to a fresh scoped consumer again.
        await using(var db=Payments())await new PaymentConsumer(db).ConsumeAsync(envelope with {DeliveryId=Guid.NewGuid()},default);
        await using(var db=Payments()){
            Assert.Equal(1,await db.Inbox.CountAsync(x=>x.MessageId==envelope.MessageId));
            Assert.Equal(1,await db.Operations.CountAsync(x=>x.Id==envelope.Payload.GetProperty("paymentOperationId").GetGuid()));
            var altered=envelope with{Payload=JsonSerializer.SerializeToElement(new ProcessPayment(envelope.Payload.GetProperty("paymentOperationId").GetGuid(),Guid.NewGuid(),"other-user",100,"MXN"),MessageCodec.Json)};
            await Assert.ThrowsAsync<PoisonMessageException>(()=>new PaymentConsumer(db).ConsumeAsync(altered,default));
        }
    }
    [SqlFact]
    public async Task SenderReceiptCommitBeforeWorkerCrashDoesNotDisappearBehindInbox()
    {
        var clock=new Clock();var reservation=Guid.NewGuid();
        var envelope=Envelope(new ReservationConfirmed(reservation,"sender-crash","Simulated confirmation"),Guid.NewGuid());
        await using(var db=Notifications())await new NotificationConsumer(db,clock).ConsumeAsync(envelope,default);
        Guid id;await using(var db=Notifications())id=(await db.Notifications.SingleAsync(x=>x.ReservationId==reservation)).Id;
        var sender=new DurableFakeSender(new Factory<NDb>(Notifications),clock);
        await using(var db=Notifications())await Assert.ThrowsAsync<Crash>(()=>new NotificationProcessor(db,sender,clock).ProcessAsync(default,id,_=>throw new Crash()));
        await using(var db=Notifications()){
            Assert.Equal("Pending",(await db.Notifications.SingleAsync(x=>x.Id==id)).Status);
            Assert.NotNull((await db.Receipts.SingleAsync(x=>x.Id==id)).DeliveredAtUtc);
        }
        clock.Now=clock.Now.AddSeconds(61);
        await using(var db=Notifications())Assert.True(await new NotificationProcessor(db,sender,clock).ProcessAsync(default,id));
        await using(var db=Notifications()){
            Assert.Equal("Sent",(await db.Notifications.SingleAsync(x=>x.Id==id)).Status);
            Assert.Equal(1,await db.Receipts.CountAsync(x=>x.Id==id));
        }
    }
    [SqlFact]
    public async Task TimeoutAfterDurableChargeIsUnknownThenReconcilesTheSameOperationWithoutAnotherCharge()
    {
        var clock=new Clock();var id=Guid.NewGuid();var saga=Guid.NewGuid();
        await using(var db=Payments()){
            var fault=await db.Faults.SingleAsync();fault.Mode="TimeoutAfterCharge";fault.Remaining=1;await db.SaveChangesAsync();
        }
        try{
            await using(var db=Payments())await new PaymentConsumer(db).ConsumeAsync(Envelope(new ProcessPayment(id,Guid.NewGuid(),"timeout-user",100,"MXN"),saga),default);
            var provider=new DurableFakeProvider(new Factory<PDb>(Payments),clock);
            await using(var db=Payments())Assert.True(await new PaymentProcessor(db,provider,clock).ProcessAsync(default,id));
            await using(var db=Payments()){
                var row=await db.Operations.SingleAsync(x=>x.Id==id);Assert.Equal("Unknown",row.Status);Assert.Equal("TimeoutAfterCharge",row.Mode);
                Assert.Equal("Succeeded",(await db.Effects.SingleAsync(x=>x.Id==id)).Status);
                var fault=await db.Faults.SingleAsync();fault.Mode="Decline";fault.Remaining=1;await db.SaveChangesAsync();
            }
            clock.Now=clock.Now.AddSeconds(6);
            await using(var db=Payments())Assert.True(await new PaymentProcessor(db,provider,clock).ProcessAsync(default,id));
            await using(var db=Payments()){
                Assert.Equal("Succeeded",(await db.Operations.SingleAsync(x=>x.Id==id)).Status);
                Assert.Equal(1,await db.Effects.CountAsync(x=>x.Id==id));
                Assert.Equal(2,await db.Outbox.CountAsync(x=>x.EffectKey.StartsWith($"payment/{id}/")));
            }
        }finally{
            await using var db=Payments();var fault=await db.Faults.SingleAsync();fault.Mode="Success";fault.Remaining=0;await db.SaveChangesAsync();
        }
    }
}
