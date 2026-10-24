using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using TourLab.Azure.Catalog.Persistence;
using TourLab.Azure.Infrastructure.Persistence;
using TourLab.Azure.Provisioning;

namespace TourLab.Azure.Tests;

[Collection("SQL Identity")]
public sealed class SqlIsolationTests
{
    [SqlTheory]
    [InlineData("identity")]
    [InlineData("catalog")]
    [InlineData("reservations")]
    [InlineData("payments")]
    [InlineData("notifications")]
    public async Task ActualRuntimePrincipalCanUseOnlyOwnedSchemaAndCannotUseDdlOrHistory(string schema)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await using var connection = new SqlConnection(TestSettings.Configuration.Runtime[schema]);
        await connection.OpenAsync(timeout.Token);
        var own = schema == "identity" ? "AspNetUsers" : "Inbox";
        Assert.Equal($"tourlab_azure_{schema}", await Scalar(connection, "SELECT USER_NAME()", timeout.Token));
        await Scalar(connection, $"SELECT COUNT(*) FROM [{schema}].[{own}]", timeout.Token);
        foreach (var other in SqlRules.Schemas.Where(value => value != schema))
        {
            var table = other == "identity" ? "AspNetUsers" : "Inbox";
            await Denied(connection, $"SELECT COUNT(*) FROM [{other}].[{table}]", timeout.Token);
            await Denied(connection, $"UPDATE [{other}].[{table}] SET [{(other == "identity" ? "UserName" : "ConsumerName")}]=[{(other == "identity" ? "UserName" : "ConsumerName")}] WHERE 1=0", timeout.Token);
        }
        await Denied(connection, $"SELECT * FROM [{schema}].[__EFMigrationsHistory]", timeout.Token);
        await Denied(connection, $"CREATE TABLE [{schema}].[Forbidden_{Guid.NewGuid():N}] (Id int)", timeout.Token);
        Assert.Equal(0, await Scalar(connection, "SELECT IS_ROLEMEMBER('db_owner')", timeout.Token));
        Assert.Equal(0, await Scalar(connection, $"SELECT HAS_PERMS_BY_NAME('{schema}','SCHEMA','ALTER')", timeout.Token));
    }

    [SqlFact]
    public async Task ExplicitSchemaDenialStillWinsAgainstInheritedPublicGrant()
    {
        var config = TestSettings.Configuration;
        await using var admin = new SqlConnection(new SqlConnectionStringBuilder(config.Admin) { InitialCatalog = config.Database }.ConnectionString);
        await admin.OpenAsync();
        await using var catalog = new SqlConnection(config.Runtime["catalog"]);
        await catalog.OpenAsync();
        try
        {
            await Scalar(admin, "GRANT SELECT ON SCHEMA::[identity] TO public", CancellationToken.None);
            await Denied(catalog, "SELECT COUNT(*) FROM [identity].[AspNetUsers]", CancellationToken.None);
        }
        finally { await Scalar(admin, "REVOKE SELECT ON SCHEMA::[identity] FROM public", CancellationToken.None); }
    }

    [SqlFact]
    public async Task RowversionRejectsStaleWriterAndUniqueInboxConflictRollsBackBeforeWinnerRead()
    {
        var options = DatabaseProvisioner.Options<CatalogDb>(TestSettings.Configuration.Runtime["catalog"], "catalog");
        var id = Guid.NewGuid();
        await using (var seed = new CatalogDb(options))
        {
            seed.Outbox.Add(Entry(id, "rowversion." + id));
            await seed.SaveChangesAsync();
        }
        await using var first = new CatalogDb(options);
        await using var second = new CatalogDb(options);
        var a = await first.Outbox.SingleAsync(row => row.DeliveryId == id);
        var b = await second.Outbox.SingleAsync(row => row.DeliveryId == id);
        Assert.Equal(8, a.RowVersion.Length);
        a.Attempts = 1; b.Attempts = 2;
        await first.SaveChangesAsync();
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync());
        var consumer = "verification-" + Guid.NewGuid().ToString("N");
        var message = Guid.NewGuid();
        first.Inbox.Add(new() { ConsumerName = consumer, MessageId = message, Fingerprint = new string('a', 64), ProcessedAtUtc = DateTime.UtcNow });
        await first.SaveChangesAsync();
        await using (var loser = new CatalogDb(options))
        {
            await using var transaction = await loser.Database.BeginTransactionAsync();
            loser.Inbox.Add(new() { ConsumerName = consumer, MessageId = message, Fingerprint = new string('a', 64), ProcessedAtUtc = DateTime.UtcNow });
            var error = await Assert.ThrowsAsync<DbUpdateException>(() => loser.SaveChangesAsync());
            Assert.True(error.InnerException is SqlException sql && SqlRules.IsUnique(sql));
            await transaction.RollbackAsync();
        }
        await using var fresh = new CatalogDb(options);
        Assert.Equal(1, await fresh.Inbox.CountAsync(row => row.ConsumerName == consumer && row.MessageId == message));
        Assert.Equal(1, (await fresh.Outbox.SingleAsync(row => row.DeliveryId == id)).Attempts);
    }

    [SqlFact]
    public async Task ConcurrentSqlClaimsDoNotOverlapAndExpiredOwnerCannotPublish()
    {
        var config = TestSettings.Configuration;
        var destination = "lease." + Guid.NewGuid().ToString("N");
        await using (var seed = new CatalogDb(DatabaseProvisioner.Options<CatalogDb>(config.Runtime["catalog"], "catalog")))
        {
            seed.Outbox.AddRange(Enumerable.Range(0, 20).Select(_ => Entry(Guid.NewGuid(), destination)));
            await seed.SaveChangesAsync();
        }
        var clock = new ManualClock(DateTimeOffset.UtcNow);
        var leases = new SqlOutboxLeases(config.Runtime["catalog"], "catalog", clock);
        var owners = new[] { Guid.NewGuid(), Guid.NewGuid() };
        var batches = await Task.WhenAll(owners.Select(owner => leases.ClaimAsync(owner, 10, TimeSpan.FromSeconds(5), destination, CancellationToken.None)));
        Assert.Equal(20, batches.SelectMany(batch => batch).Select(row => row.DeliveryId).Distinct().Count());
        Assert.All(batches, batch => Assert.Equal(10, batch.Count));
        var claimed = batches[0][0];
        Assert.False(await leases.MarkPublishedAsync(claimed.DeliveryId, Guid.NewGuid(), CancellationToken.None));
        clock.Now = clock.Now.AddSeconds(6);
        Assert.False(await leases.MarkPublishedAsync(claimed.DeliveryId, owners[0], CancellationToken.None));
        var newOwner = Guid.NewGuid();
        var reclaimed = await leases.ClaimAsync(newOwner, 50, TimeSpan.FromSeconds(5), destination, CancellationToken.None);
        Assert.Equal(20, reclaimed.Count);
        Assert.True(await leases.MarkPublishedAsync(claimed.DeliveryId, newOwner, CancellationToken.None));
        Assert.False(await leases.MarkPublishedAsync(claimed.DeliveryId, newOwner, CancellationToken.None));
    }

    [SqlFact]
    public async Task BootstrapAndMigrationAreRepeatableWithoutRecreatingUsersOrPasswords()
    {
        var config = TestSettings.Configuration;
        await using var before = new TourLab.Azure.Gateway.Identity.IdentityDb(
            DatabaseProvisioner.Options<TourLab.Azure.Gateway.Identity.IdentityDb>(config.Runtime["identity"], "identity"));
        var ids = await before.Users.AsNoTracking().OrderBy(user => user.Id).Select(user => new { user.Id, user.PasswordHash }).ToArrayAsync();
        await DatabaseProvisioner.BootstrapAsync(config, CancellationToken.None);
        await using var after = new TourLab.Azure.Gateway.Identity.IdentityDb(
            DatabaseProvisioner.Options<TourLab.Azure.Gateway.Identity.IdentityDb>(config.Runtime["identity"], "identity"));
        var repeat = await after.Users.AsNoTracking().OrderBy(user => user.Id).Select(user => new { user.Id, user.PasswordHash }).ToArrayAsync();
        Assert.Equal(ids.Select(user => user.Id), repeat.Select(user => user.Id));
        Assert.True(ids.Select(user => user.PasswordHash).SequenceEqual(repeat.Select(user => user.PasswordHash)));
        Assert.Equal(3, repeat.Length);
    }

    private static OutboxEntry Entry(Guid id, string destination) => new()
    {
        DeliveryId = id, MessageId = Guid.NewGuid(), Destination = destination, EffectKey = id.ToString(),
        Type = "Verification", EnvelopeJson = "{}", OccurredAtUtc = DateTime.UtcNow
    };
    private static async Task<object?> Scalar(SqlConnection connection, string text, CancellationToken ct)
    {
        await using var command = new SqlCommand(text, connection) { CommandTimeout = 5 };
        return await command.ExecuteScalarAsync(ct);
    }
    private static async Task Denied(SqlConnection connection, string text, CancellationToken ct)
    {
        var error = await Assert.ThrowsAsync<SqlException>(() => Scalar(connection, text, ct));
        Assert.Contains(error.Number, new[] { 229, 262, 2760, 15151 });
    }
    private sealed class ManualClock(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;
        public override DateTimeOffset GetUtcNow() => Now;
    }
}
