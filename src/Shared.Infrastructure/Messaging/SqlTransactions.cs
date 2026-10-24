using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TourLab.Azure.Contracts;
using TourLab.Azure.Infrastructure.Persistence;
namespace TourLab.Azure.Infrastructure.Messaging;

public static class SqlOperationLocks
{
    public static Task<int> AcquireAsync(DbContext db, string schema, string key, CancellationToken ct)
    {
        var principal = "tourlab_azure_" + SqlRules.Schema(schema) + "_runtime";
        return db.Database.ExecuteSqlInterpolatedAsync($"""
            DECLARE @result int;
            EXEC @result=sys.sp_getapplock @Resource={key},@LockMode='Exclusive',
                @LockOwner='Transaction',@LockTimeout=5000,@DbPrincipal={principal};
            IF @result < 0 THROW 51001, 'Operation lock unavailable.', 1;
            """, ct);
    }
}
public interface IInboxConsumer
{
    Task ConsumeAsync(IntegrationEnvelope<JsonElement> envelope, CancellationToken ct);
}
public static class InboxTransaction
{
    // Technical transport SQL only; the passed context uses the caller's restricted credential.
    public static async Task<bool> ProcessAsync(DbContext db, string schema, string consumer,
        IntegrationEnvelope<JsonElement> envelope, Func<CancellationToken, Task> effects, CancellationToken ct)
    {
        schema = SqlRules.Schema(schema);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await SqlOperationLocks.AcquireAsync(db, schema, "inbox/" + consumer + "/" + envelope.MessageId, ct);
        var fingerprint = MessageCodec.Fingerprint(envelope);
        var selectSql = $"SELECT [Fingerprint] AS [Value] FROM [{schema}].[Inbox] WHERE [ConsumerName]={{0}} AND [MessageId]={{1}}";
        var existing = await db.Database.SqlQueryRaw<string>(selectSql,
            consumer, envelope.MessageId).ToListAsync(ct);
        if (existing.Count > 0)
        {
            if (existing.Single() != fingerprint) throw new PoisonMessageException("MessageIdentityConflict");
        }
        else
        {
            await effects(ct);
            var insertSql = $"INSERT INTO [{schema}].[Inbox] ([ConsumerName],[MessageId],[Fingerprint],[ProcessedAtUtc]) VALUES ({{0}},{{1}},{{2}},{{3}})";
            await db.Database.ExecuteSqlRawAsync(insertSql,
                [consumer, envelope.MessageId, fingerprint, DateTime.UtcNow], ct);
            await db.SaveChangesAsync(ct);
        }
        await transaction.CommitAsync(ct);
        LabTelemetry.Count("inbox",existing.Count==0?"processed":"duplicate");
        return existing.Count == 0;
    }
}