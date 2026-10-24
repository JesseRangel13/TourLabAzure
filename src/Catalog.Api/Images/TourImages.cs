using Microsoft.EntityFrameworkCore;
using TourLab.Azure.Catalog.Application;
using TourLab.Azure.Catalog.Persistence;

namespace TourLab.Azure.Catalog.Images;

public sealed record TourImageView(Guid Id, Guid TourId, string ContentType, long ByteSize, string Status, long Version, string? Url, int Attempts);
public sealed class TourImages(CatalogDb db, IImageStorage storage, TimeProvider clock, ImageUploadLimit limit)
{
    private static void CheckKey(TourImage row)
    {
        var prefix = $"tours/{row.TourId:N}/";
        CatalogRules.Require(row.ObjectKey.StartsWith(prefix, StringComparison.Ordinal) &&
            Guid.TryParseExact(row.ObjectKey[prefix.Length..], "N", out _), "InvalidOwnedImageKey", 409);
    }
    private static TourImageView View(TourImage row) => new(row.Id, row.TourId, row.ContentType, row.ByteSize, row.Status, row.Version,
        row.Status == "Ready" ? $"/api/catalog/v1/tours/{row.TourId}/images/{row.Id}/content" : null, row.Attempts);
    private void History(Guid id, string outcome) => db.ImageHistory.Add(new() { ImageId = id, AtUtc = clock.GetUtcNow(), Outcome = outcome });
    private async Task Visible(Guid tour, bool admin, CancellationToken ct) =>
        CatalogRules.Require(await db.Tours.AnyAsync(t => t.Id == tour && (admin || t.Active), ct), "TourNotFound", 404);
    public async Task<TourImageView[]> ListAsync(Guid tour, bool admin, CancellationToken ct)
    {
        await Visible(tour, admin, ct);
        var rows = await db.Images.AsNoTracking().Where(x => x.TourId == tour && (admin || x.Status == "Ready"))
            .OrderBy(x => x.Status == "Deleted").ThenByDescending(x => x.CreatedAtUtc).ThenBy(x => x.Id).Take(50).ToArrayAsync(ct);
        return rows.Select(View).ToArray();
    }
    public async Task<(Stream Content, string Type)> DownloadAsync(Guid tour, Guid id, bool admin, CancellationToken ct)
    {
        await Visible(tour, admin, ct);
        var row = await db.Images.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.TourId == tour && x.Status == "Ready", ct);
        CatalogRules.Require(row is not null, "ImageNotFound", 404);
        CheckKey(row!);
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(ct);
        budget.CancelAfter(TimeSpan.FromSeconds(25));
        // Fully bounded private spool prevents unbounded storage streams after a disconnected HTTP client.
        var result = new MemoryStream((int)row!.ByteSize);
        try
        {
            await using var source = await storage.OpenAsync(row.ObjectKey, budget.Token);
            var buffer = new byte[64 * 1024];
            int read;
            while ((read = await source.ReadAsync(buffer, budget.Token)) > 0)
            {
                if (result.Length + read > row.ByteSize) throw new CatalogProblem(503, "ImageObjectConflict");
                await result.WriteAsync(buffer.AsMemory(0, read), budget.Token);
            }
            result.Position = 0;
            var hash = Convert.ToHexString(await System.Security.Cryptography.SHA256.HashDataAsync(result, budget.Token));
            if (result.Length != row.ByteSize || hash != row.Sha256) throw new CatalogProblem(503, "ImageObjectConflict");
            // Recheck after network I/O; deletion must immediately hide a previously Ready image.
            if (!await db.Images.AnyAsync(x => x.Id == id && x.Status == "Ready", budget.Token)) throw new CatalogProblem(404, "ImageNotFound");
            result.Position = 0;
            return (result, row.ContentType);
        }
        catch { await result.DisposeAsync(); throw; }
    }
    public async Task<TourImageView> UploadAsync(Guid tour, Guid id, Stream body, CancellationToken ct,
        Func<CancellationToken,Task>? afterStorage = null)
    {
        CatalogRules.Require(tour != Guid.Empty && id != Guid.Empty, "InvalidImageId");
        if (!await limit.Gate.WaitAsync(0, ct)) throw new CatalogProblem(429, "ImageUploadBusy");
        try
        {
            using var budget = CancellationTokenSource.CreateLinkedTokenSource(ct);
            budget.CancelAfter(TimeSpan.FromSeconds(40));
            await using var image = await ValidatedImage.ReadAsync(body, budget.Token);
            var owner = Guid.NewGuid(); var now = clock.GetUtcNow();
            TourImage row;
            await using (var tx = await db.Database.BeginTransactionAsync(budget.Token))
            {
                await CatalogRules.LockAsync(db, $"images/tour/{tour}", budget.Token);
                await CatalogRules.LockAsync(db, $"images/id/{id}", budget.Token);
                await Visible(tour, true, budget.Token);
                row = await db.Images.SingleOrDefaultAsync(x => x.Id == id, budget.Token) ?? new TourImage {
                    Id = id, TourId = tour, ObjectKey = $"tours/{tour:N}/{Guid.NewGuid():N}",
                    ContentType = image.ContentType, ByteSize = image.Content.Length, Sha256 = image.Sha256, CreatedAtUtc = now };
                CatalogRules.Require(row.TourId == tour, "ImageNotFound", 404);
                CatalogRules.Require(row.Sha256 == image.Sha256, "ImageIntentionChanged", 409);
                if (row.Status == "Ready") return View(row);
                CatalogRules.Require(row.Status == "Pending", "ImageDeletionRequested", 409);
                CatalogRules.Require(row.LeaseUntilUtc is null || row.LeaseUntilUtc <= now, "ImageUploadInProgress", 409);
                if (db.Entry(row).State == EntityState.Detached)
                {
                    CatalogRules.Require(await db.Images.CountAsync(x => x.TourId == tour && x.Status != "Deleted", budget.Token) < 3, "ImageCountLimit", 409);
                    db.Images.Add(row);
                }
                row.LeaseOwner = owner; row.LeaseUntilUtc = now.AddMinutes(2); row.Version++; row.Attempts++;
                History(id, "UploadClaimed");
                await db.SaveChangesAsync(budget.Token); await tx.CommitAsync(budget.Token);
            }
            CheckKey(row);
            await storage.PutAsync(row.ObjectKey, image.Content, image.ContentType, image.Sha256, budget.Token);
            var stored = await storage.HeadAsync(row.ObjectKey, budget.Token);
            if (stored != new StoredImage(row.ByteSize, row.Sha256, row.ContentType)) throw new CatalogProblem(503, "ImageObjectConflict");
            if (afterStorage is not null) await afterStorage(budget.Token);
            await FinishAsync(row.Id, owner, "Pending", "Ready", budget.Token);
            db.ChangeTracker.Clear();
            return View(await db.Images.AsNoTracking().SingleAsync(x => x.Id == id, budget.Token));
        }
        finally { limit.Gate.Release(); }
    }
    public async Task DeleteAsync(Guid tour, Guid id, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var changed = await db.Images.Where(x => x.Id == id && x.TourId == tour)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, x => x.Status == "Deleted" ? "Deleted" : "DeletePending")
                .SetProperty(x => x.Version, x => x.Version + 1), ct);
        CatalogRules.Require(changed != 0, "ImageNotFound", 404);
        History(id, "DeleteRequested"); await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        // Keep the upload lease. A late PUT can never publish Ready; tombstones are periodically swept.
    }
    public async Task<bool> ReconcileAsync(CancellationToken ct, Guid? selected = null)
    {
        var now = clock.GetUtcNow(); var owner = Guid.NewGuid();
        TourImage? row;
        await using (var tx = await db.Database.BeginTransactionAsync(ct))
        {
            row = await db.Images.FromSqlInterpolated($"""
                SELECT TOP (1) * FROM [catalog].[TourImages] WITH (UPDLOCK, READPAST, READCOMMITTEDLOCK)
                WHERE [Status] IN ('Pending','DeletePending','Deleted') AND ([LeaseUntilUtc] IS NULL OR [LeaseUntilUtc]<={now.UtcDateTime})
                AND ({selected} IS NULL OR [Id]={selected}) ORDER BY [LeaseUntilUtc], [Id]
                """).FirstOrDefaultAsync(ct);
            if (row is null) return false;
            row.LeaseOwner = owner; row.LeaseUntilUtc = now.AddMinutes(2); row.Version++; row.Attempts++;
            History(row.Id, "RecoveryClaimed"); await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        }
        CheckKey(row);
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(ct); budget.CancelAfter(TimeSpan.FromSeconds(25));
        try
        {
            if (row.Status is "DeletePending" or "Deleted")
            {
                await storage.DeleteAsync(row.ObjectKey, budget.Token);
                await FinishAsync(row.Id, owner, row.Status, "Deleted", ct);
            }
            else
            {
                var stored = await storage.HeadAsync(row.ObjectKey, budget.Token);
                var to = stored == new StoredImage(row.ByteSize, row.Sha256, row.ContentType) ? "Ready"
                    : stored is not null || row.CreatedAtUtc <= now.AddMinutes(-15) ? "DeletePending" : "Pending";
                await FinishAsync(row.Id, owner, "Pending", to, ct);
            }
        }
        catch
        {
            // Safe diagnostic only; lease expires and the next scoped worker retries this intent.
            using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            db.ChangeTracker.Clear(); History(row.Id, "RecoveryFailed");
            await db.SaveChangesAsync(cleanup.Token); throw;
        }
        return true;
    }
    private async Task FinishAsync(Guid id, Guid owner, string from, string to, CancellationToken ct)
    {
        db.ChangeTracker.Clear();
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var now = clock.GetUtcNow();
        var changed = await db.Images.Where(x => x.Id == id && x.Status == from && x.LeaseOwner == owner && x.LeaseUntilUtc > now)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, to).SetProperty(x => x.Version, x => x.Version + 1)
                .SetProperty(x => x.LeaseOwner, (Guid?)null)
                .SetProperty(x => x.LeaseUntilUtc, to == "Ready" ? (DateTimeOffset?)null : to == "Deleted" ? now.AddHours(1) : now.AddSeconds(15)), ct);
        History(id, changed == 0 ? "CompletionSuperseded" : to);
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
    }
}
