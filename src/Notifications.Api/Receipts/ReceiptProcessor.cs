using Microsoft.Extensions.Logging;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TourLab.Azure.Contracts;
using TourLab.Azure.Infrastructure;
using TourLab.Azure.Infrastructure.Messaging;
using TourLab.Azure.Notifications.Persistence;
namespace TourLab.Azure.Notifications.Receipts;

// Scoped per invocation. SQL claim commits before Blob I/O; finalize checks lease owner AND version.
public sealed class ReceiptProcessor(NotificationsDb db, IReceiptStorage storage, TimeProvider clock, IReceiptInvocationHook? hook = null, ILogger<ReceiptProcessor>? logger = null)
{
    public async Task ProcessAsync(IntegrationEnvelope<JsonElement> envelope, CancellationToken ct,
        Func<CancellationToken,Task>? afterBlob = null)
    {
        var command = MessageCodec.Payload<GenerateBookingReceipt>(envelope);
        if (command.JobId == Guid.Empty) throw new PoisonMessageException("InvalidReceiptJob");
        ReceiptJob job;
        Guid owner = Guid.NewGuid();
        long version;
        using var span = LabTelemetry.Start("receipt.generate", System.Diagnostics.ActivityKind.Consumer, envelope.Traceparent, envelope.Tracestate);
        span?.SetTag("OperationId", command.JobId);
        db.ChangeTracker.Clear();
        await using (var tx = await db.Database.BeginTransactionAsync(ct))
        {
            await SqlOperationLocks.AcquireAsync(db, "notifications", "receipt/" + command.JobId, ct);
            job = await db.ReceiptJobs.SingleOrDefaultAsync(x => x.Id == command.JobId, ct)
                ?? throw new PoisonMessageException("ReceiptJobNotFound");
            if (job.SagaId != envelope.SagaId) throw new PoisonMessageException("ReceiptSagaConflict");
            span?.SetTag("ReservationId", job.ReservationId);
            if (job.Status == "Conflict") throw new PoisonMessageException("ReceiptObjectConflict");
            if (job.LeaseUntilUtc > clock.GetUtcNow()) throw new ReceiptLeaseBusy();
            job.LeaseOwner = owner; job.LeaseUntilUtc = clock.GetUtcNow().AddSeconds(60);
            job.Version++; job.Attempts++; version = job.Version;
            Audit(job.Id, "Claimed");
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        }
        try
        {
            var snapshot = JsonSerializer.Deserialize<ConfirmationSnapshot>(job.SnapshotJson, MessageCodec.Json)
                ?? throw new ReceiptObjectConflict();
            var bytes = ReceiptDocument.Render(job.ReservationId, snapshot);
            if (job.BlobKey != ReceiptDocument.Key(job.ReservationId, snapshot.SourceBusinessVersion) ||
                job.SourceBusinessVersion != snapshot.SourceBusinessVersion || job.ConfirmedAtUtc != snapshot.ConfirmedAtUtc ||
                ReceiptDocument.Hash(bytes) != job.ContentHash) throw new ReceiptObjectConflict();
            using var budget = CancellationTokenSource.CreateLinkedTokenSource(ct);
            budget.CancelAfter(TimeSpan.FromSeconds(30));
            await storage.EnsureAsync(job.BlobKey, bytes, job.ContentHash, budget.Token);
            if (hook is not null) await hook.AfterBlobAsync(job.Id, ct);
            if (afterBlob is not null) await afterBlob(ct);
            await FinishAsync(job.Id, owner, version, "Ready", null, ct);
            LabTelemetry.Count("receipt", "Ready");
            logger?.LogInformation("Receipt {OperationId} reservation {ReservationId} Ready at source version {BusinessVersion}; TraceId {TraceId} SpanId {SpanId}", job.Id, job.ReservationId, job.SourceBusinessVersion, span?.TraceId.ToString(), span?.SpanId.ToString());
        }
        catch (ReceiptObjectConflict)
        {
            await FinishAsync(job.Id, owner, version, "Conflict", "ReceiptObjectConflict", ct);
            throw new PoisonMessageException("ReceiptObjectConflict");
        }
        // Other errors leave the committed lease/job intact; broker redelivery retries after expiry.
    }
    private async Task FinishAsync(Guid id, Guid owner, long version, string status, string? error, CancellationToken ct)
    {
        db.ChangeTracker.Clear();
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await SqlOperationLocks.AcquireAsync(db, "notifications", "receipt/" + id, ct);
        var job = await db.ReceiptJobs.SingleAsync(x => x.Id == id, ct);
        if (job.LeaseOwner != owner || job.Version != version || job.LeaseUntilUtc <= clock.GetUtcNow())
            throw new ReceiptLeaseBusy();
        job.Status = status; job.ErrorCode = error; job.ReadyAtUtc = status == "Ready" ? job.ReadyAtUtc ?? clock.GetUtcNow() : null;
        job.LeaseOwner = null; job.LeaseUntilUtc = null; job.Version++;
        Audit(id, status);
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
    }
    private void Audit(Guid job, string outcome) => db.ReceiptAttempts.Add(new() {
        Id = Guid.NewGuid(), JobId = job, Outcome = outcome, OccurredAtUtc = clock.GetUtcNow()
    });
}
public sealed class ReceiptLeaseBusy : Exception;
public interface IReceiptInvocationHook { Task AfterBlobAsync(Guid jobId, CancellationToken ct); }
