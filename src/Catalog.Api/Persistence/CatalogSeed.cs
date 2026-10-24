using Microsoft.EntityFrameworkCore;
using TourLab.Azure.Catalog.Application;

namespace TourLab.Azure.Catalog.Persistence;

// New synthetic Azure-only identities; no original database rows or resource IDs are imported.
public static class CatalogSeed
{
    public static readonly Guid FirstTourId = Guid.Parse("a3000000-0000-4000-8000-000000000001");
    public static readonly Guid FirstSessionId = Guid.Parse("a3010000-0000-4000-8000-000000000001");
    public static async Task RunAsync(CatalogDb db, TimeProvider clock, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await CatalogRules.LockAsync(db, "catalog/azure-synthetic-seed", ct);
        foreach (var (tourId, sessionId, name, amount) in new[] {
            (FirstTourId, FirstSessionId, "Azure lab city walk", 25000L),
            (Guid.Parse("a3000000-0000-4000-8000-000000000002"), Guid.Parse("a3010000-0000-4000-8000-000000000002"),
                "Azure lab mountain visit", 80000L) })
        {
            var tour = await db.Tours.SingleOrDefaultAsync(t => t.Id == tourId, ct);
            if (tour is null)
            {
                tour = new Tour { Id = tourId, Name = name, Description = "Synthetic educational tour. Booking arrives in AZ-004." };
                db.Tours.Add(tour);
            }
            if (!await db.Sessions.AnyAsync(s => s.Id == sessionId, ct))
            {
                var session = new TourSession {
                    Id = sessionId, TourId = tourId, StartsAtUtc = clock.GetUtcNow().AddDays(30),
                    UnitAmountMinor = amount, Capacity = 12, AvailableSeats = 12
                };
                db.Sessions.Add(session);
                await CatalogRules.ProjectionAsync(db, tour, session, clock.GetUtcNow(), ct);
            }
        }
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }
}