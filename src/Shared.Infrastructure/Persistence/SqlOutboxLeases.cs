using System.Data;
using Microsoft.Data.SqlClient;

namespace TourLab.Azure.Infrastructure.Persistence;

public sealed record ClaimedDelivery(Guid DeliveryId, Guid MessageId, string Destination, string EnvelopeJson);

// Technical SQL plumbing only. The service supplies its own runtime credential and owns its Outbox entity.
public sealed class SqlOutboxLeases(string connectionString, string schema, TimeProvider clock)
{
    private readonly string ownedSchema = SqlRules.Schema(schema);

    public async Task<IReadOnlyList<ClaimedDelivery>> ClaimAsync(
        Guid owner, int batchSize, TimeSpan lease, string? destination, CancellationToken ct)
    {
        if (owner == Guid.Empty || batchSize is < 1 or > 50 || lease < TimeSpan.FromSeconds(1) ||
            lease > TimeSpan.FromMinutes(5) || destination?.Length > 128)
            throw new ArgumentException("Invalid bounded lease request.");
        var now = clock.GetUtcNow().UtcDateTime;
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(ct);
        // READCOMMITTEDLOCK makes READPAST valid with Azure SQL's READ_COMMITTED_SNAPSHOT setting.
        await using var command = new SqlCommand($"""
            ;WITH due AS (
                SELECT TOP (@batch) * FROM [{ownedSchema}].[Outbox]
                WITH (UPDLOCK, READPAST, READCOMMITTEDLOCK)
                WHERE [PublishedAtUtc] IS NULL AND ([LeaseUntilUtc] IS NULL OR [LeaseUntilUtc] <= @now)
                  AND (@destination IS NULL OR [Destination]=@destination)
                ORDER BY [OccurredAtUtc], [DeliveryId]
            )
            UPDATE due SET [LeaseOwner]=@owner,[LeaseUntilUtc]=@until,[Attempts]=[Attempts]+1
            OUTPUT inserted.[DeliveryId],inserted.[MessageId],inserted.[Destination],inserted.[EnvelopeJson];
            """, connection) { CommandTimeout = 5 };
        command.Parameters.Add("@batch", SqlDbType.Int).Value = batchSize;
        command.Parameters.Add("@now", SqlDbType.DateTime2).Value = now;
        command.Parameters.Add("@until", SqlDbType.DateTime2).Value = now.Add(lease);
        command.Parameters.Add("@owner", SqlDbType.UniqueIdentifier).Value = owner;
        command.Parameters.Add("@destination", SqlDbType.NVarChar, 128).Value = (object?)destination ?? DBNull.Value;
        var result = new List<ClaimedDelivery>();
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            result.Add(new(reader.GetGuid(0), reader.GetGuid(1), reader.GetString(2), reader.GetString(3)));
        return result;
    }

    public async Task<bool> MarkPublishedAsync(Guid deliveryId, Guid owner, CancellationToken ct)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(ct);
        await using var command = new SqlCommand($"""
            UPDATE [{ownedSchema}].[Outbox] SET [PublishedAtUtc]=@now,[LeaseOwner]=NULL,[LeaseUntilUtc]=NULL
            WHERE [DeliveryId]=@id AND [LeaseOwner]=@owner AND [LeaseUntilUtc]>@now AND [PublishedAtUtc] IS NULL;
            """, connection) { CommandTimeout = 5 };
        command.Parameters.Add("@now", SqlDbType.DateTime2).Value = clock.GetUtcNow().UtcDateTime;
        command.Parameters.Add("@id", SqlDbType.UniqueIdentifier).Value = deliveryId;
        command.Parameters.Add("@owner", SqlDbType.UniqueIdentifier).Value = owner;
        return await command.ExecuteNonQueryAsync(ct) == 1;
    }
}
