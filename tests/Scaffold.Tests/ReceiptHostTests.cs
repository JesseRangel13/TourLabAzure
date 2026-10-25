using System.Text.Json;
using Azure.Messaging.ServiceBus;
using Microsoft.EntityFrameworkCore;
using TourLab.Azure.Contracts;
using TourLab.Azure.Infrastructure.Messaging;
using TourLab.Azure.Infrastructure.Persistence;
using TourLab.Azure.Notifications.Receipts;
namespace TourLab.Azure.Tests;
public sealed class ReceiptHostFactAttribute:FactAttribute
{
    public ReceiptHostFactAttribute(){if(Environment.GetEnvironmentVariable("AZURELAB_RECEIPT_HOST")!="1")
        Skip="NOT VERIFIED: run Verify-Receipts.ps1 actual Functions host stages.";}
}
[Collection("SQL Identity")]
public sealed class ReceiptHostTests
{
    private const string Bus="Endpoint=sb://localhost:5673;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;";
    private sealed record Fixture(Guid Job,string Key,string Hash,string Command);
    private static string FixturePath=>Path.Combine(TestSettings.Output,"receipt-host-fixture.json");
    [ReceiptHostFact]
    public async Task StageDurableOutboxForActualHostCrashAfterBlob()
    {
        var(job,command)=await ReceiptIntegrationTests.Accept();
        var worker=Path.Combine(TestSettings.Output,"receipts.json");
        var config=JsonSerializer.Deserialize<Dictionary<string,JsonElement>>(await File.ReadAllTextAsync(worker))!;
        config["ReceiptTestBarrier"]=JsonSerializer.SerializeToElement(new{JobId=job.Id});
        await File.WriteAllTextAsync(worker,JsonSerializer.Serialize(config));
        var leases=new SqlOutboxLeases(TestSettings.Configuration.Runtime["notifications"],"notifications",TimeProvider.System);
        var owner=Guid.NewGuid();var delivery=Assert.Single(await leases.ClaimAsync(owner,1,TimeSpan.FromSeconds(60),"tourlab-azure-receipt-jobs",default));
        Assert.Equal(command.MessageId,MessageCodec.Parse(delivery.EnvelopeJson).MessageId);
        await using var bus=new ServiceBusClient(Bus);await using var sender=bus.CreateSender(delivery.Destination);
        await sender.SendMessageAsync(new ServiceBusMessage(delivery.EnvelopeJson){MessageId=delivery.MessageId.ToString("D")});
        Assert.True(await leases.MarkPublishedAsync(delivery.DeliveryId,owner,default));
        await File.WriteAllTextAsync(FixturePath,JsonSerializer.Serialize(new Fixture(job.Id,job.BlobKey,job.ContentHash,delivery.EnvelopeJson)));
    }
    [ReceiptHostFact]
    public async Task CrashedActualInvocationHasBlobButNoReadySqlAndSafeRetainedLease()
    {
        var f=JsonSerializer.Deserialize<Fixture>(await File.ReadAllTextAsync(FixturePath))!;
        Assert.Equal(f.Job.ToString("D"),await File.ReadAllTextAsync(Path.Combine(TestSettings.Output,"receipt-after-blob.marker")));
        await using var db=BookingSqlTests.Notifications();var job=await db.ReceiptJobs.SingleAsync(x=>x.Id==f.Job);
        Assert.Equal("Pending",job.Status);Assert.NotNull(job.LeaseOwner);Assert.Null(job.ReadyAtUtc);
        Assert.Equal(f.Hash,ReceiptDocument.Hash(await ReceiptIntegrationTests.Storage().ReadAsync(f.Key,f.Hash,default)));
        var worker=Path.Combine(TestSettings.Output,"receipts.json");
        var config=JsonSerializer.Deserialize<Dictionary<string,JsonElement>>(await File.ReadAllTextAsync(worker))!;
        config.Remove("ReceiptTestBarrier");await File.WriteAllTextAsync(worker,JsonSerializer.Serialize(config));
    }
    [ReceiptHostFact]
    public async Task RestartedActualHostRecoversRedeliveryThenDuplicateAndPoisonAreSettledSafely()
    {
        var f=JsonSerializer.Deserialize<Fixture>(await File.ReadAllTextAsync(FixturePath))!;
        using var budget=new CancellationTokenSource(TimeSpan.FromSeconds(140));
        var blob=BlobReceiptStorage.Create(ReceiptIntegrationTests.Settings()).GetBlobClient(f.Key);
        var etag=(await blob.GetPropertiesAsync(cancellationToken:budget.Token)).Value.ETag;
        await WaitReady(f.Job,budget.Token);
        Assert.Equal(etag,(await blob.GetPropertiesAsync(cancellationToken:budget.Token)).Value.ETag);
        await using var bus=new ServiceBusClient(Bus);await using var sender=bus.CreateSender("tourlab-azure-receipt-jobs");
        int attempts;await using(var db=BookingSqlTests.Notifications())attempts=(await db.ReceiptJobs.SingleAsync(x=>x.Id==f.Job)).Attempts;
        await sender.SendMessageAsync(new ServiceBusMessage(f.Command){MessageId=Guid.NewGuid().ToString("D")},budget.Token);
        while(true){
            await using var db=BookingSqlTests.Notifications();var row=await db.ReceiptJobs.SingleAsync(x=>x.Id==f.Job,budget.Token);
            if(row.Status=="Ready"&&row.Attempts>attempts)break;
            await Task.Delay(200,budget.Token);
        }
        Assert.Equal(etag,(await blob.GetPropertiesAsync(cancellationToken:budget.Token)).Value.ETag);
        var poisonId=Guid.NewGuid().ToString("D");
        await sender.SendMessageAsync(new ServiceBusMessage("{invalid"){MessageId=poisonId},budget.Token);
        await using var dlq=bus.CreateReceiver("tourlab-azure-receipt-jobs",new ServiceBusReceiverOptions{SubQueue=SubQueue.DeadLetter});
        await WaitDlq(dlq,poisonId,"InvalidJson",budget.Token);
        var(conflict,command)=await ReceiptIntegrationTests.Accept();
        var contradictory=BlobReceiptStorage.Create(ReceiptIntegrationTests.Settings()).GetBlobClient(conflict.BlobKey);
        await contradictory.UploadAsync(BinaryData.FromString("Retained conflicting host-test document"),cancellationToken:budget.Token);
        var preserved=(await contradictory.GetPropertiesAsync(cancellationToken:budget.Token)).Value.ETag;
        var conflictId=Guid.NewGuid().ToString("D");
        await sender.SendMessageAsync(new ServiceBusMessage(JsonSerializer.Serialize(command,MessageCodec.Json)){MessageId=conflictId},budget.Token);
        await WaitDlq(dlq,conflictId,"ReceiptObjectConflict",budget.Token);
        Assert.Equal(preserved,(await contradictory.GetPropertiesAsync(cancellationToken:budget.Token)).Value.ETag);
        await using(var db=BookingSqlTests.Notifications()){
            Assert.Equal("Conflict",(await db.ReceiptJobs.SingleAsync(x=>x.Id==conflict.Id,budget.Token)).Status);
            Assert.True(await db.ReceiptAttempts.AnyAsync(x=>x.JobId==conflict.Id&&x.Outcome=="Conflict",budget.Token));
            Assert.Equal(1,await db.ReceiptJobs.CountAsync(x=>x.Id==f.Job,budget.Token));
        }
    }
    private static async Task WaitReady(Guid id,CancellationToken ct){
        while(true){await using var db=BookingSqlTests.Notifications();
            if((await db.ReceiptJobs.SingleAsync(x=>x.Id==id,ct)).Status=="Ready")return;
            await Task.Delay(200,ct);
        }
    }
    private static async Task WaitDlq(ServiceBusReceiver dlq,string id,string reason,CancellationToken ct){
        long sequence=0;
        while(true){
            var batch=await dlq.PeekMessagesAsync(20,sequence,ct);
            var found=batch.SingleOrDefault(x=>x.MessageId==id);
            if(found is not null){Assert.Equal(reason,found.DeadLetterReason);return;}
            if(batch.Count>0)sequence=batch.Max(x=>x.SequenceNumber)+1;
            await Task.Delay(200,ct);
        }
    }
}