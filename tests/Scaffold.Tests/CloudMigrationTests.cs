using TourLab.Azure.Provisioning;
namespace TourLab.Azure.Tests;
public sealed class CloudMigrationTests
{
    [Theory]
    [InlineData("catalog","foreign.database.windows.net","tourlab_azure_study")]
    [InlineData("catalog","tourlabazure-study1-sql.database.windows.net","other")]
    [InlineData("other","tourlabazure-study1-sql.database.windows.net","tourlab_azure_study")]
    [InlineData("catalog","tourlabazure-study1-sql.database.windows.net;TrustServerCertificate=True","tourlab_azure_study")]
    public void Rejects_unowned_or_injected_cloud_targets(string schema,string server,string db) =>
        Assert.Throws<ArgumentException>(()=>CloudMigration.ValidateTarget(schema,server,db,Guid.NewGuid()));
    [Theory]
    [InlineData("GO 10\nSELECT 1;")]
    [InlineData(":connect foreign\nSELECT 1;")]
    [InlineData("SELECT '$(SQLPASSWORD)';")]
    public void Rejects_sqlcmd_execution_directives(string sql) => Assert.Throws<ArgumentException>(()=>CloudMigration.Batches(sql));
    [Fact]
    public void Preserves_transaction_across_EF_batch_separators()
    {
        var batches=CloudMigration.Batches("BEGIN TRANSACTION;\nGO\nSELECT 'GO stays in a string';\r\nGO\r\nCOMMIT;\nGO\n");
        Assert.Equal(3,batches.Length);
        Assert.Contains("BEGIN TRANSACTION",batches[0]);Assert.Contains("GO stays in a string",batches[1]);Assert.Contains("COMMIT",batches[2]);
    }

    [Theory]
    [InlineData("https://tourlabazure-study1-gateway-abc123.eastus-01.azurewebsites.net/",true)]
    [InlineData("https://tourlabazure-study1-gateway.azurewebsites.net/",true)]
    [InlineData("https://foreign-abc123.eastus-01.azurewebsites.net/",false)]
    [InlineData("https://tourlabazure-study1.eastus-01.azurewebsites.net.evil.invalid/",false)]
    [InlineData("https://tourlabazure-study1.eastus-01.evil.azurewebsites.net/",false)]
    [InlineData("https://tourlabazure-study1.eastus-01.azurewebsites.net/?token=bad",false)]
    [InlineData("http://tourlabazure-study1.eastus-01.azurewebsites.net/",false)]
    [InlineData("https://tourlabazure-study1.eastus-01.azurewebsites.net:9443/",false)]
    public void Platform_hostnames_preserve_prefix_TLS_origin_and_suffix_boundaries(string address,bool expected) =>
        Assert.Equal(expected,TourLab.Azure.Infrastructure.Security.AzureSecurity.Endpoint(address,"azurewebsites.net"));
}
