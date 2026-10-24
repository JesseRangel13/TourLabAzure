using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;


namespace TourLab.Azure.Catalog.Application;

// Infrastructure only: no shared EF entity or business table access.
public sealed class TransactionOwner : IAsyncDisposable
{
    private readonly IDbContextTransaction? owned;

    private TransactionOwner(IDbContextTransaction? transaction)
    {
        owned = transaction;
    }

    public static async Task<TransactionOwner> BeginAsync(DbContext db, CancellationToken ct) =>
        new(db.Database.CurrentTransaction is null
            ? await db.Database.BeginTransactionAsync(ct)
            : null);

    public Task CommitAsync(CancellationToken ct) =>
        owned?.CommitAsync(ct) ?? Task.CompletedTask;

    public ValueTask DisposeAsync() =>
        owned?.DisposeAsync() ?? ValueTask.CompletedTask;
}

