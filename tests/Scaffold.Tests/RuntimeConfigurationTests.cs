using Microsoft.Data.SqlClient;
using TourLab.Azure.Infrastructure.Persistence;

namespace TourLab.Azure.Tests;

public sealed class RuntimeConfigurationTests
{
    [Fact]
    public void LocalCredentialsAreBoundedAndDevelopmentOnly()
    {
        var runtime = new SqlConnectionStringBuilder(TestSettings.Configuration.Runtime["catalog"]);
        Assert.True(SqlRules.RuntimeSafe(new() { ConnectionString = runtime.ConnectionString, Schema = "catalog", Development = true }));
        runtime.Encrypt = SqlConnectionEncryptOption.Mandatory;
        runtime.TrustServerCertificate = false;
        Assert.False(SqlRules.RuntimeSafe(new() { ConnectionString = runtime.ConnectionString, Schema = "catalog", Development = false }));
        runtime.UserID = "sa";
        Assert.False(SqlRules.RuntimeSafe(new() { ConnectionString = runtime.ConnectionString, Schema = "catalog", Development = true }));
        runtime.UserID = "tourlab_azure_catalog"; runtime.MaxPoolSize = 5;
        var localServer=runtime.DataSource;
        runtime.DataSource="tourlabazure-study.database.windows.net,1433";
        runtime.Encrypt=SqlConnectionEncryptOption.Strict;
        Assert.False(SqlRules.RuntimeSafe(new(){ConnectionString=runtime.ConnectionString,Schema="catalog",Development=true}));
        runtime.DataSource=localServer;runtime.Authentication=SqlAuthenticationMethod.ActiveDirectoryDefault;
        Assert.False(SqlRules.RuntimeSafe(new(){ConnectionString=runtime.ConnectionString,Schema="catalog",Development=true}));
        runtime.Authentication=SqlAuthenticationMethod.NotSpecified;
        runtime.UserID = "tourlab_azure_catalog"; runtime.MaxPoolSize = 100;
        Assert.False(SqlRules.RuntimeSafe(new() { ConnectionString = runtime.ConnectionString, Schema = "catalog", Development = true }));
    }

    [Fact]
    public void UtcMappingRejectsUnspecifiedOrLocalTimeAndReadsUtc()
    {
        var converter = new UtcDateTimeConverter();
        Assert.Throws<ArgumentException>(() => converter.ConvertToProvider(new DateTime(2026, 10, 23)));
        var utc = DateTime.UtcNow;
        Assert.Equal(utc, converter.ConvertToProvider(utc));
        Assert.Equal(DateTimeKind.Utc, ((DateTime)converter.ConvertFromProvider(DateTime.SpecifyKind(utc, DateTimeKind.Unspecified))!).Kind);
    }
}
