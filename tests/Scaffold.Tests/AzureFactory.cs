using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using TourLab.Azure.Provisioning;

namespace TourLab.Azure.Tests;

internal static class TestSettings
{
    public static readonly string Root;
    public static readonly string Output;
    public static readonly ProvisioningConfiguration Configuration;

    static TestSettings()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "TourLab.Azure.slnx"))) directory = directory.Parent;
        Root = directory?.FullName ?? throw new InvalidOperationException("Azure solution root is required.");
        var integration = Environment.GetEnvironmentVariable("AZURELAB_INTEGRATION_CONFIG");
        if (!string.IsNullOrWhiteSpace(integration)) Output = Path.GetDirectoryName(Path.GetFullPath(integration))!;
        else
        {
            Output = Path.Combine(Root, ".local", "unit-" + Guid.NewGuid().ToString("N"));
            PrivateConfiguration.Initialize(Root, Output, "tourlab_azure_unit_" + Guid.NewGuid().ToString("N"), "LocalDb");
        }
        Configuration = PrivateConfiguration.Read(Path.Combine(Output, "provisioner.json"));
        // Gateway uses standard certificate registration at startup, before test-service overrides.
        Environment.SetEnvironmentVariable("AZURELAB_CONFIG", Path.Combine(Output, "gateway.json"));
    }
}

public sealed class AzureFactory<T> : WebApplicationFactory<T> where T : class
{
    private readonly string schema;
    public AzureFactory(string schema) { _ = TestSettings.Output; this.schema = schema; }
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        var directory = TestSettings.Output;
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, config) => config.AddJsonFile(
            Path.Combine(directory, schema == "identity" ? "gateway.json" : $"{schema}.json")));
    }
}

public sealed class SqlFactAttribute : FactAttribute
{
    public SqlFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("AZURELAB_INTEGRATION_CONFIG")))
            Skip = "NOT VERIFIED: set AZURELAB_INTEGRATION_CONFIG through Verify-SqlIdentity.ps1 to run real SQL Server tests.";
    }
}
public sealed class SqlTheoryAttribute : TheoryAttribute
{
    public SqlTheoryAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("AZURELAB_INTEGRATION_CONFIG")))
            Skip = "NOT VERIFIED: real SQL Server integration configuration is required.";
    }
}
[CollectionDefinition("SQL Identity", DisableParallelization = true)]
public sealed class SqlIdentityCollection;
