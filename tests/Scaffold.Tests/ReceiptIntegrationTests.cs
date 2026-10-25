using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Azure.Storage.Blobs.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TourLab.Azure.Contracts;
using TourLab.Azure.Infrastructure.Messaging;
using TourLab.Azure.Notifications.Application;
using TourLab.Azure.Notifications.Receipts;
namespace TourLab.Azure.Tests;
public sealed class ReceiptFactAttribute:FactAttribute
{
    public ReceiptFactAttribute(){if(Environment.GetEnvironmentVariable("AZURELAB_RECEIPTS")!="1"||
        string.IsNullOrEmpty(Environment.GetEnvironmentVariable("AZURELAB_INTEGRATION_CONFIG")))
        Skip="NOT VERIFIED: run Verify-Receipts.ps1 against real SQL and Azurite.";}
}
[Collection("SQL Identity")]
public sealed class ReceiptIntegrationTests
{
    internal static ReceiptStorageOptions Settings()=>JsonSerializer.Deserialize<ReceiptStorageOptions>(
        JsonDocument.Parse(File.ReadAllText(Path.Combine(TestSettings.Output,"notifications.json"))).RootElement.GetProperty("ReceiptStorage").GetRawText())!;
    internal static BlobReceiptStorage Storage()=>new(Options.Create(Settings()));
    internal static async Task<(ReceiptJob Job,IntegrationEnvelope<JsonElement> Command)> Accept(string user="receipt-owner")
    {
        var confirmed=BookingSqlTests.Envelope(new ReservationConfirmed(Guid.NewGuid(),user,"SIMULATED confirmation",ReceiptUnitTests.Snapshot()),Guid.NewGuid());
        await using(var db=BookingSqlTests.Notifications())await new NotificationConsumer(db,TimeProvider.System).ConsumeAsync(confirmed,default);
        await using(var db=BookingSqlTests.Notifications()){
            var reservation=confirmed.Payload.GetProperty("reservationId").GetGuid();
            var job=await db.ReceiptJobs.AsNoTracking().SingleAsync(x=>x.ReservationId==reservation);
            var command=await db.Outbox.SingleAsync(x=>x.EffectKey=="receipt/"+job.Id);
            return(job,MessageCodec.Parse(command.EnvelopeJson));
        }
    }
    [ReceiptFact]
    public async Task AcceptanceIsAtomicDuplicateSafeAndRejectsContradictorySnapshot()
    {
        var confirmation=new ReservationConfirmed(Guid.NewGuid(),"atomic-owner","SIMULATED",ReceiptUnitTests.Snapshot());
        var source=BookingSqlTests.Envelope(confirmation,Guid.NewGuid());
        await using(var db=BookingSqlTests.Notifications())await new NotificationConsumer(db,TimeProvider.System).ConsumeAsync(source,default);
        await using(var db=BookingSqlTests.Notifications())await new NotificationConsumer(db,TimeProvider.System).ConsumeAsync(source with{DeliveryId=Guid.NewGuid()},default);
        var contradictory=source with {MessageId=Guid.NewGuid(),Payload=JsonSerializer.SerializeToElement(
            confirmation with{Confirmation=confirmation.Confirmation! with{AmountMinor=999}},MessageCodec.Json)};
        await using(var db=BookingSqlTests.Notifications())await Assert.ThrowsAsync<PoisonMessageException>(()=>
            new NotificationConsumer(db,TimeProvider.System).ConsumeAsync(contradictory,default));
        await using(var db=BookingSqlTests.Notifications()){
            var job=Assert.Single(await db.ReceiptJobs.Where(x=>x.ReservationId==confirmation.ReservationId).ToListAsync());
            Assert.Equal(1,await db.Notifications.CountAsync(x=>x.ReservationId==confirmation.ReservationId));
            Assert.Equal(1,await db.Outbox.CountAsync(x=>x.EffectKey=="receipt/"+job.Id));
            Assert.False(await db.Inbox.AnyAsync(x=>x.MessageId==contradictory.MessageId));
            Assert.Equal(ReceiptDocument.Hash(ReceiptDocument.Render(confirmation.ReservationId,confirmation.Confirmation!)),job.ContentHash);
        }
        var invalid=BookingSqlTests.Envelope(confirmation with{ReservationId=Guid.NewGuid(),Confirmation=ReceiptUnitTests.Snapshot() with{Currency="USD"}},Guid.NewGuid());
        await using(var db=BookingSqlTests.Notifications())await Assert.ThrowsAsync<PoisonMessageException>(()=>
            new NotificationConsumer(db,TimeProvider.System).ConsumeAsync(invalid,default));
        await using(var db=BookingSqlTests.Notifications())Assert.False(await db.Inbox.AnyAsync(x=>x.MessageId==invalid.MessageId));
    }
    [ReceiptFact]
    public async Task ActualPrivateBlobDuplicateAndOwnerAdminDownloadEnforceAuthorization()
    {
        var (job,command)=await Accept();
        await using(var db=BookingSqlTests.Notifications())await new ReceiptProcessor(db,Storage(),TimeProvider.System).ProcessAsync(command,default);
        var container=BlobReceiptStorage.Create(Settings());
        var first=await container.GetBlobClient(job.BlobKey).GetPropertiesAsync();
        await using(var db=BookingSqlTests.Notifications())await new ReceiptProcessor(db,Storage(),TimeProvider.System).ProcessAsync(command with{DeliveryId=Guid.NewGuid()},default);
        Assert.Equal(first.Value.ETag,(await container.GetBlobClient(job.BlobKey).GetPropertiesAsync()).Value.ETag);
        Assert.Equal(PublicAccessType.None,(await container.GetAccessPolicyAsync()).Value.BlobPublicAccess);
        using(var anonymous=new HttpClient())Assert.False((await anonymous.GetAsync(container.GetBlobClient(job.BlobKey).Uri)).IsSuccessStatusCode);
        await using var factory=new AzureFactory<TourLab.Azure.Notifications.Program>("notifications");
        using var owner=RecoverySqlTests.Client(factory,"Notifications","Tourist","receipt-owner");
        using var bob=RecoverySqlTests.Client(factory,"Notifications","Tourist","other-owner");
        using var admin=RecoverySqlTests.Client(factory,"Notifications","Admin","admin");
        using var download=await owner.GetAsync("/v1/receipts/"+job.Id+"/content");
        Assert.Equal(HttpStatusCode.OK,download.StatusCode);Assert.Equal("attachment",download.Content.Headers.ContentDisposition!.DispositionType);
        Assert.True(download.Headers.CacheControl!.NoStore);Assert.Equal("nosniff",download.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Contains("SIMULATED EDUCATIONAL",await download.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.NotFound,(await bob.GetAsync("/v1/receipts/"+job.Id+"/content")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,(await bob.GetAsync("/v1/receipts/reservation/"+job.ReservationId)).StatusCode);
        Assert.Equal(HttpStatusCode.OK,(await admin.GetAsync("/v1/receipts/"+job.Id+"/content")).StatusCode);
        using var anonymousApi=factory.CreateClient();Assert.Equal(HttpStatusCode.Unauthorized,(await anonymousApi.GetAsync("/v1/receipts/"+job.Id+"/content")).StatusCode);
    }
    [ReceiptFact]
    public async Task BlobSuccessThenSqlFailureNewInvocationAndLostOwnerCannotDuplicateOrFinalize()
    {
        var (job,command)=await Accept();var clock=new BookingSqlTests.Clock();var actual=Storage();
        await using(var db=BookingSqlTests.Notifications())await Assert.ThrowsAsync<BookingSqlTests.Crash>(()=>
            new ReceiptProcessor(db,actual,clock).ProcessAsync(command,default,_=>throw new BookingSqlTests.Crash()));
        var blob=BlobReceiptStorage.Create(Settings()).GetBlobClient(job.BlobKey);var etag=(await blob.GetPropertiesAsync()).Value.ETag;
        await using(var db=BookingSqlTests.Notifications()){
            Assert.Equal("Pending",(await db.ReceiptJobs.SingleAsync(x=>x.Id==job.Id)).Status);
            await Assert.ThrowsAsync<ReceiptLeaseBusy>(()=>new ReceiptProcessor(db,actual,clock).ProcessAsync(command,default));
        }
        clock.Now=clock.Now.AddSeconds(61);
        await using(var db=BookingSqlTests.Notifications())await new ReceiptProcessor(db,actual,clock).ProcessAsync(command,default);
        Assert.Equal(etag,(await blob.GetPropertiesAsync()).Value.ETag);
        var (lost,lostCommand)=await Accept();
        await using(var db=BookingSqlTests.Notifications())await Assert.ThrowsAsync<ReceiptLeaseBusy>(()=>
            new ReceiptProcessor(db,actual,clock).ProcessAsync(lostCommand,default,async _=>{
                await using var other=BookingSqlTests.Notifications();var row=await other.ReceiptJobs.SingleAsync(x=>x.Id==lost.Id);
                row.LeaseOwner=Guid.NewGuid();row.Version++;await other.SaveChangesAsync();
            }));
        await using(var db=BookingSqlTests.Notifications())Assert.Equal("Pending",(await db.ReceiptJobs.SingleAsync(x=>x.Id==lost.Id)).Status);
    }
    [ReceiptFact]
    public async Task ConcurrentClaimsCreateOneDocumentAndConflictingBytesAreAuditedWithoutOverwrite()
    {
        var (job,command)=await Accept();var actual=Storage();var entered=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var boundary=new BarrierStorage(actual,async()=>{entered.SetResult();await release.Task;});
        await using var first=BookingSqlTests.Notifications();
        var running=new ReceiptProcessor(first,boundary,TimeProvider.System).ProcessAsync(command,default);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        try{await using var second=BookingSqlTests.Notifications();await Assert.ThrowsAsync<ReceiptLeaseBusy>(()=>new ReceiptProcessor(second,actual,TimeProvider.System).ProcessAsync(command,default));}
        finally{release.SetResult();}
        await running;
        var (conflict,conflictCommand)=await Accept();
        var blob=BlobReceiptStorage.Create(Settings()).GetBlobClient(conflict.BlobKey);
        await blob.UploadAsync(BinaryData.FromString("Preserved contradictory object"));
        var etag=(await blob.GetPropertiesAsync()).Value.ETag;
        await using(var db=BookingSqlTests.Notifications())Assert.Equal("ReceiptObjectConflict",(await Assert.ThrowsAsync<PoisonMessageException>(()=>
            new ReceiptProcessor(db,actual,TimeProvider.System).ProcessAsync(conflictCommand,default))).Message);
        Assert.Equal(etag,(await blob.GetPropertiesAsync()).Value.ETag);
        await using(var db=BookingSqlTests.Notifications()){
            Assert.Equal("Conflict",(await db.ReceiptJobs.SingleAsync(x=>x.Id==conflict.Id)).Status);
            Assert.True(await db.ReceiptAttempts.AnyAsync(x=>x.JobId==conflict.Id&&x.Outcome=="Conflict"));
        }
    }
    [ReceiptFact]
    public async Task ReceiptOutageDoesNotBlockDurableSenderAndCancellationKeepsHistoricalSnapshot()
    {
        var (job,command)=await Accept();
        await using(var db=BookingSqlTests.Notifications())await Assert.ThrowsAsync<IOException>(()=>
            new ReceiptProcessor(db,new OutageStorage(),TimeProvider.System).ProcessAsync(command,default));
        Guid notification;
        await using(var db=BookingSqlTests.Notifications())notification=(await db.Notifications.SingleAsync(x=>x.ReservationId==job.ReservationId)).Id;
        var sender=new DurableFakeSender(new BookingSqlTests.Factory<TourLab.Azure.Notifications.Persistence.NotificationsDb>(BookingSqlTests.Notifications),TimeProvider.System);
        await using(var db=BookingSqlTests.Notifications())Assert.True(await new NotificationProcessor(db,sender,TimeProvider.System).ProcessAsync(default,notification));
        var cancelled=BookingSqlTests.Envelope(new ReservationCancelled(job.ReservationId,job.UserId,"SIMULATED cancellation"),job.SagaId);
        await using(var db=BookingSqlTests.Notifications())await new NotificationConsumer(db,TimeProvider.System).ConsumeAsync(cancelled,default);
        await using(var db=BookingSqlTests.Notifications()){
            var retained=await db.ReceiptJobs.SingleAsync(x=>x.Id==job.Id);
            Assert.Equal(job.SnapshotJson,retained.SnapshotJson);Assert.Equal(job.ContentHash,retained.ContentHash);
            Assert.Equal("Sent",(await db.Notifications.SingleAsync(x=>x.Id==notification)).Status);
            Assert.Equal(1,await db.ReceiptJobs.CountAsync(x=>x.ReservationId==job.ReservationId));
        }
    }
    [ReceiptFact]
    public async Task ActualSagaConfirmationCarriesAcceptedSnapshotAndBusinessVersionWithoutCrossSchemaRuntimeAccess()
    {
        await using var flow=new RecoverySqlTests.Flow();await flow.Init();await flow.Confirmed();
        var envelope=await flow.Intent("reservations","ReservationConfirmed");
        var confirmed=MessageCodec.Payload<ReservationConfirmed>(envelope);
        await using(var db=BookingSqlTests.Reservations()){
            var reservation=await db.Reservations.SingleAsync(x=>x.Id==flow.Id);
            Assert.NotNull(confirmed.Confirmation);
            Assert.Equal(reservation.Version,confirmed.Confirmation.SourceBusinessVersion);
            Assert.Equal(reservation.TotalAmountMinor,confirmed.Confirmation.AmountMinor);
            Assert.Equal(reservation.TourName,confirmed.Confirmation.TourName);
            Assert.Equal("Confirmed",reservation.Status);
        }
        await using(var db=BookingSqlTests.Notifications())await new NotificationConsumer(db,flow.Clock).ConsumeAsync(envelope,default);
        await using(var db=BookingSqlTests.Notifications()){
            Assert.Equal(1,await db.ReceiptJobs.CountAsync(x=>x.ReservationId==flow.Id));
            // This same restricted principal cannot retrieve Reservations' business snapshot itself.
            await Assert.ThrowsAsync<Microsoft.Data.SqlClient.SqlException>(()=>
                db.Database.ExecuteSqlRawAsync("SELECT TOP(1) Id FROM reservations.Reservations"));
        }
    }
    [ReceiptFact]
    public async Task LegacyVersionOneConfirmationStillDeliversButCannotInventAnImmutableReceipt()
    {
        var confirmation=new ReservationConfirmed(Guid.NewGuid(),"legacy-owner","Historical SIMULATED event without accepted snapshot");
        var envelope=BookingSqlTests.Envelope(confirmation,Guid.NewGuid());
        await using(var db=BookingSqlTests.Notifications())await new NotificationConsumer(db,TimeProvider.System).ConsumeAsync(envelope,default);
        await using(var db=BookingSqlTests.Notifications())await new NotificationConsumer(db,TimeProvider.System).ConsumeAsync(envelope with{DeliveryId=Guid.NewGuid()},default);
        await using(var db=BookingSqlTests.Notifications()){
            Assert.Equal(1,await db.Notifications.CountAsync(x=>x.ReservationId==confirmation.ReservationId));
            Assert.False(await db.ReceiptJobs.AnyAsync(x=>x.ReservationId==confirmation.ReservationId));
            Assert.Equal(1,await db.Audit.CountAsync(x=>x.Action=="LegacyConfirmationWithoutSnapshot/"+confirmation.ReservationId));
        }
    }
    private sealed class BarrierStorage(IReceiptStorage inner,Func<Task> barrier):IReceiptStorage
    {
        public async Task EnsureAsync(string key,byte[] bytes,string hash,CancellationToken ct){await barrier();await inner.EnsureAsync(key,bytes,hash,ct);}
        public Task<byte[]> ReadAsync(string key,string hash,CancellationToken ct)=>inner.ReadAsync(key,hash,ct);
    }
    private sealed class OutageStorage:IReceiptStorage
    {
        public Task EnsureAsync(string key,byte[] bytes,string hash,CancellationToken ct)=>throw new IOException("Test-only receipt outage");
        public Task<byte[]> ReadAsync(string key,string hash,CancellationToken ct)=>throw new IOException("Test-only receipt outage");
    }
}