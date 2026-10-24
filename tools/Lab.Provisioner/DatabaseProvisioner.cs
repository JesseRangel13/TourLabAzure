using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TourLab.Azure.Infrastructure.Persistence;
using TourLab.Azure.Gateway.Identity;
using TourLab.Azure.Catalog.Persistence;
using TourLab.Azure.Reservations.Persistence;
using TourLab.Azure.Payments.Persistence;
using TourLab.Azure.Notifications.Persistence;

namespace TourLab.Azure.Provisioning;

public static class DatabaseProvisioner
{
    public static async Task BootstrapAsync(ProvisioningConfiguration config, CancellationToken ct)
    {
        Validate(config);
        await using var master = new SqlConnection(config.Admin);
        await master.OpenAsync(ct);
        await ExecuteAsync(master, "EXEC sp_configure 'show advanced options',1; RECONFIGURE; EXEC sp_configure 'contained database authentication',1; RECONFIGURE;", ct);
        await using (var exists = new SqlCommand("SELECT DB_ID(@name)", master))
        {
            exists.Parameters.AddWithValue("@name", config.Database);
            if (await exists.ExecuteScalarAsync(ct) is DBNull)
            {
                await ExecuteAsync(master, $"CREATE DATABASE [{config.Database}] COLLATE Latin1_General_100_BIN2;", ct);
                await ExecuteAsync(master, $"ALTER DATABASE [{config.Database}] SET CONTAINMENT=PARTIAL; ALTER DATABASE [{config.Database}] SET READ_COMMITTED_SNAPSHOT ON; ALTER DATABASE [{config.Database}] SET COMPATIBILITY_LEVEL=160;", ct);
                await using var created = AdminDatabase(config);
                await created.OpenAsync(ct);
                await ExecuteAsync(created, "EXEC sys.sp_addextendedproperty @name=N'TourLabAzure.Project',@value=N'TourLabAzure';", ct);
            }
        }
        await using var database = AdminDatabase(config);
        await database.OpenAsync(ct);
        await using (var tag = new SqlCommand("SELECT CONVERT(nvarchar(128),[value]) FROM sys.extended_properties WHERE class=0 AND name=N'TourLabAzure.Project'", database))
            if ((string?)await tag.ExecuteScalarAsync(ct) != "TourLabAzure")
                throw new InvalidOperationException("Existing database is not tagged for this lab; refusing to modify it.");

        await ExecuteAsync(database, "REVOKE CONNECT FROM guest; ALTER DATABASE CURRENT SET TRUSTWORTHY OFF;", ct);
        // Establish every isolated schema before installing cross-schema DENY entries.
        foreach (var schema in SqlRules.Schemas)
        {
            var owner = $"tourlab_azure_{schema}_owner";
            await ExecuteAsync(database, $"IF DATABASE_PRINCIPAL_ID(N'{owner}') IS NULL CREATE USER [{owner}] WITHOUT LOGIN; IF SCHEMA_ID(N'{schema}') IS NULL EXEC(N'CREATE SCHEMA [{schema}] AUTHORIZATION [{owner}]');", ct);
        }
        foreach (var schema in SqlRules.Schemas)
        {
            var owner = $"tourlab_azure_{schema}_owner";
            var runtime = $"tourlab_azure_{schema}";
            var migrator = $"{runtime}_migrator";
            var role = $"{runtime}_runtime";
            await ExecuteAsync(database, $"""
                IF DATABASE_PRINCIPAL_ID(N'{owner}') IS NULL CREATE USER [{owner}] WITHOUT LOGIN;
                IF SCHEMA_ID(N'{schema}') IS NULL EXEC(N'CREATE SCHEMA [{schema}] AUTHORIZATION [{owner}]');
                IF DATABASE_PRINCIPAL_ID(N'{role}') IS NULL CREATE ROLE [{role}] AUTHORIZATION dbo;
                """, ct);
            await CreateContainedAsync(database, runtime, new SqlConnectionStringBuilder(config.Runtime[schema]).Password, schema, ct);
            await CreateContainedAsync(database, migrator, new SqlConnectionStringBuilder(config.Migrator[schema]).Password, schema, ct);
            await ExecuteAsync(database, $"""
                IF IS_ROLEMEMBER(N'{role}',N'{runtime}')<>1 ALTER ROLE [{role}] ADD MEMBER [{runtime}];
                GRANT CONNECT TO [{runtime}];
                GRANT CONNECT TO [{migrator}];
                GRANT SELECT,INSERT,UPDATE,DELETE ON SCHEMA::[{schema}] TO [{role}];
                GRANT CREATE TABLE TO [{migrator}];
                GRANT ALTER,REFERENCES,SELECT,INSERT,UPDATE,DELETE ON SCHEMA::[{schema}] TO [{migrator}];
                DENY CREATE TABLE,CREATE VIEW,CREATE PROCEDURE,CREATE FUNCTION,CREATE SCHEMA,ALTER ANY USER,ALTER ANY ROLE TO [{role}];
                DENY ALTER,TAKE OWNERSHIP ON SCHEMA::[{schema}] TO [{role}];
                REVOKE SELECT,INSERT,UPDATE,DELETE,EXECUTE,REFERENCES ON SCHEMA::[{schema}] FROM public;
                """, ct);
            foreach (var other in SqlRules.Schemas.Where(other => other != schema))
                await ExecuteAsync(database, $"""
                    DENY SELECT,INSERT,UPDATE,DELETE,EXECUTE,REFERENCES,ALTER,TAKE OWNERSHIP ON SCHEMA::[{other}] TO [{role}];
                    DENY ALTER ON SCHEMA::[{other}] TO [{migrator}];
                    """, ct);
        }

        foreach (var schema in SqlRules.Schemas)
        {
            await using var ownership = new SqlCommand(
                "SELECT USER_NAME(principal_id) FROM sys.schemas WHERE name=@schema", database);
            ownership.Parameters.AddWithValue("@schema", schema);
            if ((string?)await ownership.ExecuteScalarAsync(ct) != $"tourlab_azure_{schema}_owner")
                throw new InvalidOperationException("Unexpected schema ownership; provisioning refused.");
        }
        await MigrateAsync(config, ct);
        foreach (var schema in SqlRules.Schemas)
            await ExecuteAsync(database, $"DENY SELECT,INSERT,UPDATE,DELETE ON OBJECT::[{schema}].[__EFMigrationsHistory] TO [tourlab_azure_{schema}_runtime];", ct);
        await SeedAsync(config, ct);
        await using var catalogSeed = new CatalogDb(Options<CatalogDb>(config.Runtime["catalog"], "catalog"));
        await CatalogSeed.RunAsync(catalogSeed, TimeProvider.System, ct);
    }

    private static async Task MigrateAsync(ProvisioningConfiguration config, CancellationToken ct)
    {
        await using var identity = new IdentityDb(Options<IdentityDb>(config.Migrator["identity"], "identity"));
        await identity.Database.MigrateAsync(ct);
        await using var catalog = new CatalogDb(Options<CatalogDb>(config.Migrator["catalog"], "catalog"));
        await catalog.Database.MigrateAsync(ct);
        await using var reservations = new ReservationsDb(Options<ReservationsDb>(config.Migrator["reservations"], "reservations"));
        await reservations.Database.MigrateAsync(ct);
        await using var payments = new PaymentsDb(Options<PaymentsDb>(config.Migrator["payments"], "payments"));
        await payments.Database.MigrateAsync(ct);
        await using var notifications = new NotificationsDb(Options<NotificationsDb>(config.Migrator["notifications"], "notifications"));
        await notifications.Database.MigrateAsync(ct);
    }

    public static DbContextOptions<T> Options<T>(string connection, string schema) where T : DbContext =>
        new DbContextOptionsBuilder<T>().UseSqlServer(connection, sql =>
        {
            sql.MigrationsHistoryTable("__EFMigrationsHistory", SqlRules.Schema(schema));
            sql.CommandTimeout(10);
        }).Options;

    private static async Task SeedAsync(ProvisioningConfiguration config, CancellationToken ct)
    {
        var services = new ServiceCollection();
        services.AddLogging(logs => logs.SetMinimumLevel(LogLevel.None));
        services.AddSingleton(new ProvisioningCancellation(ct));
        services.AddDbContext<IdentityDb>(options => options.UseSqlServer(config.Migrator["identity"],
            sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", "identity")));
        services.AddIdentityCore<LabUser>(options => options.Password.RequiredLength = 12)
            .AddRoles<IdentityRole>().AddEntityFrameworkStores<IdentityDb>().AddUserManager<SeedUserManager>().AddRoleManager<SeedRoleManager>();
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<LabUser>>();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var name in new[] { "Tourist", "Admin" })
        {
            ct.ThrowIfCancellationRequested();
            if (!await roles.RoleExistsAsync(name)) Check(await roles.CreateAsync(new IdentityRole(name)));
        }
        foreach (var seed in config.Users)
        {
            ct.ThrowIfCancellationRequested();
            var user = await users.FindByNameAsync(seed.UserName);
            if (user is null)
            {
                user = new LabUser { UserName = seed.UserName, Email = $"{seed.UserName.ToLowerInvariant()}@tourlab-azure.invalid", EmailConfirmed = true, LockoutEnabled = true };
                Check(await users.CreateAsync(user, seed.Password));
            }
            if (!await users.IsInRoleAsync(user, seed.Role)) Check(await users.AddToRoleAsync(user, seed.Role));
        }
    }

    private static void Check(IdentityResult result)
    {
        if (!result.Succeeded) throw new InvalidOperationException("Identity seed operation failed; inspect safe error codes.");
    }

    private static async Task CreateContainedAsync(SqlConnection connection, string name, string password, string schema, CancellationToken ct)
    {
        // All identifiers are fixed/validated; generated passwords are restricted before interpolation.
        if (!Regex.IsMatch(password, "^[A-Za-z0-9!]{20,128}$")) throw new ArgumentException("Unexpected private password format.");
        await ExecuteAsync(connection, $"IF DATABASE_PRINCIPAL_ID(N'{name}') IS NULL CREATE USER [{name}] WITH PASSWORD=N'{password}',DEFAULT_SCHEMA=[{schema}];", ct);
    }

    private static SqlConnection AdminDatabase(ProvisioningConfiguration config)
    {
        var value = new SqlConnectionStringBuilder(config.Admin) { InitialCatalog = config.Database };
        return new SqlConnection(value.ConnectionString);
    }

    private static async Task ExecuteAsync(SqlConnection connection, string sql, CancellationToken ct)
    {
        await using var command = new SqlCommand(sql, connection) { CommandTimeout = 30 };
        await command.ExecuteNonQueryAsync(ct);
    }

    private static void Validate(ProvisioningConfiguration config)
    {
        if (!Regex.IsMatch(config.Database, "^tourlab_azure(?:_[a-zA-Z0-9]+)*$")) throw new ArgumentException("Database prefix refused.");
        var admin = new SqlConnectionStringBuilder(config.Admin);
        if (admin.InitialCatalog != "master" || !new[] { @"(localdb)\tourlab-azure", "127.0.0.1,14339" }.Contains(admin.DataSource, StringComparer.OrdinalIgnoreCase))
            throw new ArgumentException("Only the isolated local SQL instance can be provisioned. Cloud execution is separate.");
        foreach (var schema in SqlRules.Schemas)
        {
            var runtime = new SqlConnectionStringBuilder(config.Runtime[schema]);
            var migrator = new SqlConnectionStringBuilder(config.Migrator[schema]);
            if (!SqlRules.RuntimeSafe(new() { ConnectionString = runtime.ConnectionString, Schema = schema, Development = true }) ||
                runtime.DataSource != admin.DataSource || runtime.InitialCatalog != config.Database ||
                migrator.DataSource != admin.DataSource || migrator.InitialCatalog != config.Database ||
                migrator.IntegratedSecurity || migrator.UserID != $"tourlab_azure_{schema}_migrator")
                throw new ArgumentException("Runtime/migration ownership boundary refused.");
        }
        if (config.Users.Length != 3 || !config.Users.Select(x => x.UserName).Order().SequenceEqual(new[] { "Admin", "Alice", "Bob" }) ||
            config.Users.Any(user => user.Role != (user.UserName == "Admin" ? "Admin" : "Tourist")))
            throw new ArgumentException("Only the three synthetic seed identities are supported.");
    }
}
