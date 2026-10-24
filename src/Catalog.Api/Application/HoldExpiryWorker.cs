using Microsoft.EntityFrameworkCore;
using TourLab.Azure.Catalog.Persistence;

namespace TourLab.Azure.Catalog.Application;

public sealed class HoldExpiryWorker(IServiceScopeFactory scopes, TimeProvider clock, ILogger<HoldExpiryWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(5), clock);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                // Scope per batch; no context survives a timer tick. Each expiry owns a fresh scope/transaction.
                Guid[] ids;
                await using (var scope = scopes.CreateAsyncScope())
                {
                    var db = scope.ServiceProvider.GetRequiredService<CatalogDb>();
                    var now = clock.GetUtcNow();
                    ids = await db.Holds.AsNoTracking().Where(h => h.Status == "Held" && h.ExpiresAtUtc <= now)
                        .OrderBy(h => h.ExpiresAtUtc).ThenBy(h => h.Id).Take(50).Select(h => h.Id).ToArrayAsync(stoppingToken);
                }
                foreach (var id in ids)
                {
                    await using var scope = scopes.CreateAsyncScope();
                    await scope.ServiceProvider.GetRequiredService<InventoryHandlers>().ExpireAsync(id, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception error) { logger.LogWarning("Hold expiry batch deferred: {FailureType}", error.GetType().Name); }
        }
    }
}