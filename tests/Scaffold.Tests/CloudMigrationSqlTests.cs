using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using TourLab.Azure.Provisioning;
using TourLab.Azure.Gateway.Identity;
using TourLab.Azure.Catalog.Persistence;
using TourLab.Azure.Reservations.Persistence;
using TourLab.Azure.Payments.Persistence;
using TourLab.Azure.Notifications.Persistence;
namespace TourLab.Azure.Tests;
[Collection("SQL Identity")]
public sealed class CloudMigrationSqlTests
{
    [SqlFact]
    public async Task Five_schema_scripts_rerun_twice_as_bounded_migrators_without_granting_runtime_DDL()
    {
        using var timeout=new CancellationTokenSource(TimeSpan.FromMinutes(2));var ct=timeout.Token;
        var config=TestSettings.Configuration;
        await using var identity=new IdentityDb(DatabaseProvisioner.Options<IdentityDb>(config.Migrator["identity"],"identity"));
        await using var catalog=new CatalogDb(DatabaseProvisioner.Options<CatalogDb>(config.Migrator["catalog"],"catalog"));
        await using var reservations=new ReservationsDb(DatabaseProvisioner.Options<ReservationsDb>(config.Migrator["reservations"],"reservations"));
        await using var payments=new PaymentsDb(DatabaseProvisioner.Options<PaymentsDb>(config.Migrator["payments"],"payments"));
        await using var notifications=new NotificationsDb(DatabaseProvisioner.Options<NotificationsDb>(config.Migrator["notifications"],"notifications"));
        foreach(var (schema,context) in new (string,DbContext)[]{("identity",identity),("catalog",catalog),("reservations",reservations),("payments",payments),("notifications",notifications)}){
            var before=(await context.Database.GetAppliedMigrationsAsync(ct)).ToArray();Assert.NotEmpty(before);
            var release=Environment.GetEnvironmentVariable("AZURELAB_RELEASE_DIRECTORY");
            var sql=release is null?context.GetService<IMigrator>().GenerateScript(options:MigrationsSqlGenerationOptions.Idempotent):await File.ReadAllTextAsync(Path.Combine(release,schema+".sql"),ct);
            await using var connection=new SqlConnection(config.Migrator[schema]);await connection.OpenAsync(ct);
            await CloudMigration.ExecuteBatchesAsync(connection,sql,ct);
            await CloudMigration.ExecuteBatchesAsync(connection,sql,ct);
            Assert.Equal(before,(await context.Database.GetAppliedMigrationsAsync(ct)).ToArray());
            await using var runtime=new SqlConnection(config.Runtime[schema]);await runtime.OpenAsync(ct);
            await using var permission=new SqlCommand("SELECT HAS_PERMS_BY_NAME(DB_NAME(),'DATABASE','CREATE TABLE')",runtime);
            Assert.Equal(0,Convert.ToInt32(await permission.ExecuteScalarAsync(ct)));
            await using var history=new SqlCommand($"SELECT COUNT(*) FROM [{schema}].[__EFMigrationsHistory]",runtime);
            await Assert.ThrowsAsync<SqlException>(async()=>await history.ExecuteScalarAsync(ct));
        }
    }
}
