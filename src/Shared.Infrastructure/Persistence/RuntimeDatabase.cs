using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace TourLab.Azure.Infrastructure.Persistence;

public sealed class RuntimeDatabase
{
    public string ConnectionString { get; set; } = "";
    public string Schema { get; set; } = "";
    public bool Development { get; set; }
}

public static class SqlRules
{
    public static readonly string[] Schemas = ["identity", "catalog", "reservations", "payments", "notifications"];
    public static string Schema(string schema) =>
        Schemas.Contains(schema, StringComparer.Ordinal) ? schema : throw new ArgumentException("Unknown owned schema.");

    public static bool RuntimeSafe(RuntimeDatabase settings)
    {
        try
        {
            Schema(settings.Schema);
            var value = new SqlConnectionStringBuilder(settings.ConnectionString);
            if (value.IntegratedSecurity || value.UserID != $"tourlab_azure_{settings.Schema}" ||
                !value.InitialCatalog.StartsWith("tourlab_azure", StringComparison.Ordinal) ||
                !value.Pooling || value.MaxPoolSize is < 1 or > 5 || value.ConnectTimeout is < 1 or > 5)
                return false;
            var localDb = value.DataSource.Equals(@"(localdb)\tourlab-azure", StringComparison.OrdinalIgnoreCase);
            var localTcp = value.DataSource.Equals("127.0.0.1,14339", StringComparison.OrdinalIgnoreCase);
            var host = value.DataSource.Split(',')[0];
            var loopback = host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
                (System.Net.IPAddress.TryParse(host, out var ip) && System.Net.IPAddress.IsLoopback(ip));
            if (!settings.Development && (loopback || value.DataSource.Contains("(localdb)", StringComparison.OrdinalIgnoreCase) ||
                value.DataSource.StartsWith("np:", StringComparison.OrdinalIgnoreCase))) return false;
            if (settings.Development && (localDb || localTcp))
                return localDb || value.Encrypt != SqlConnectionEncryptOption.Optional;
            return value.Encrypt != SqlConnectionEncryptOption.Optional && !value.TrustServerCertificate;
        }
        catch (ArgumentException) { return false; }
    }

    public static bool IsUnique(SqlException exception) => exception.Number is 2601 or 2627;
}

public static class RuntimeDatabaseRegistration
{
    public static void AddRuntimeDatabase(this IServiceCollection services, string schema)
    {
        SqlRules.Schema(schema);
        services.AddOptions<RuntimeDatabase>()
            .Configure<IConfiguration, IHostEnvironment>((options, config, environment) =>
            {
                options.ConnectionString = config.GetConnectionString("Runtime") ?? "";
                options.Schema = schema;
                options.Development = environment.IsDevelopment();
            })
            .Validate(SqlRules.RuntimeSafe, "Runtime SQL configuration must use its bounded schema principal; local exceptions are Development-only.")
            .ValidateOnStart();
        services.AddSingleton(TimeProvider.System);
    }

    public static void MapDatabaseReadiness(this WebApplication app, string policy)
    {
        app.MapGet("/health/ready", async (IOptions<RuntimeDatabase> settings, CancellationToken ct) =>
        {
            await using var connection = new SqlConnection(settings.Value.ConnectionString);
            try
            {
                await connection.OpenAsync(ct);
                await using var command = new SqlCommand(
                    "SELECT CASE WHEN USER_NAME()=@user AND IS_ROLEMEMBER(@role)=1 THEN 1 ELSE 0 END", connection) { CommandTimeout = 3 };
                command.Parameters.AddWithValue("@user", $"tourlab_azure_{settings.Value.Schema}");
                command.Parameters.AddWithValue("@role", $"tourlab_azure_{settings.Value.Schema}_runtime");
                var ready = (int)(await command.ExecuteScalarAsync(ct) ?? 0) == 1;
                return ready ? Results.Ok(new { status = "Ready" }) :
                    Results.Problem(statusCode: 503, title: "Owned database principal is not ready", extensions: new Dictionary<string, object?> { ["code"] = "DatabaseNotReady" });
            }
            catch (SqlException)
            {
                return Results.Problem(statusCode: 503, title: "Owned database is unavailable", extensions: new Dictionary<string, object?> { ["code"] = "DatabaseUnavailable" });
            }
        }).RequireAuthorization(policy);
    }
}
