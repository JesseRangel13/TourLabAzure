using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TourLab.Azure.Infrastructure.Security;
using TourLab.Azure.Gateway.Identity;
using TourLab.Azure.Provisioning;
namespace TourLab.Azure.Tests;

[Collection("SQL Identity")]
public sealed class ProtectionSqlTests
{
    [SqlFact]
    public async Task DurableSqlRingSurvivesProviderRestartAndCertificateRolloverWithoutPlaintextKeys()
    {
        var folder=Path.Combine(TestSettings.Root,".local","protection-"+Guid.NewGuid().ToString("N"));
        PrivateConfiguration.Initialize(TestSettings.Root,folder,"tourlab_azure_protection_"+Guid.NewGuid().ToString("N"),TestSettings.Configuration.Profile);
        var configuration=PrivateConfiguration.Read(Path.Combine(folder,"provisioner.json"));
        await DatabaseProvisioner.BootstrapAsync(configuration,default);
        var activeSettings=new ConfigurationManager();activeSettings.AddJsonFile(Path.Combine(folder,"gateway.json"));
        using var old=ProtectionCertificates.Load(activeSettings.GetSection("DataProtection"),true);
        string prior;
        using(var services=Services(configuration.Runtime["identity"],old)){
            prior=services.GetRequiredService<IDataProtectionProvider>().CreateProtector("rotation-check").Protect("owner-cookie");
        }
        using var rsa=RSA.Create(3072);
        var request=new CertificateRequest("CN=TourLab Azure rotation test",rsa,HashAlgorithmName.SHA256,RSASignaturePadding.Pkcs1);
        using var cert=request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1),DateTimeOffset.UtcNow.AddYears(1));
        var password="Az9!"+Convert.ToHexString(RandomNumberGenerator.GetBytes(32));var next=Path.Combine(folder,"next-protection.pfx");
        await File.WriteAllBytesAsync(next,cert.Export(X509ContentType.Pfx,password));
        var rotation=new{CertificatePath=next,CertificatePassword=password,PreviousCertificates=new[]{
            new{Path=activeSettings["DataProtection:CertificatePath"],Password=activeSettings["DataProtection:CertificatePassword"]}}};
        var path=Path.Combine(folder,"rotation-settings.json");await File.WriteAllTextAsync(path,JsonSerializer.Serialize(rotation));
        var settings=new ConfigurationManager();settings.AddJsonFile(path);
        string after;
        using(var certificates=ProtectionCertificates.Load(settings,true))
        using(var services=Services(configuration.Runtime["identity"],certificates)){
            var protector=services.GetRequiredService<IDataProtectionProvider>().CreateProtector("rotation-check");
            Assert.Equal("owner-cookie",protector.Unprotect(prior));
            services.GetRequiredService<IKeyManager>().CreateNewKey(DateTimeOffset.UtcNow,DateTimeOffset.UtcNow.AddDays(90));
            after=protector.Protect("next-cookie");
        }
        using(var certificates=ProtectionCertificates.Load(settings,true))
        using(var services=Services(configuration.Runtime["identity"],certificates)){
            var protector=services.GetRequiredService<IDataProtectionProvider>().CreateProtector("rotation-check");
            Assert.Equal("owner-cookie",protector.Unprotect(prior));Assert.Equal("next-cookie",protector.Unprotect(after));
        }
        await using var db=new IdentityDb(DatabaseProvisioner.Options<IdentityDb>(configuration.Runtime["identity"],"identity"));
        var keys=await db.DataProtectionKeys.ToArrayAsync();Assert.True(keys.Length>=2);
        Assert.Contains(keys,key=>key.Xml!.Contains(Convert.ToBase64String(cert.RawData),StringComparison.Ordinal));
        Assert.Contains(keys,key=>key.Xml!.Contains(Convert.ToBase64String(old.Active.RawData),StringComparison.Ordinal));
        Assert.All(keys,key=>{Assert.Contains("encryptedSecret",key.Xml!);Assert.DoesNotContain("BEGIN PRIVATE KEY",key.Xml!);});
        Assert.Throws<SecurityConfigurationException>(()=>ProtectionCertificates.Load(settings,false));
    }
    [ReceiptFact]
    public async Task ReceiptRoleCanFinalizeItsJobAndInsertAuditButCannotReadOutboxOrOtherBusinessTables()
    {
        var (job,command)=await ReceiptIntegrationTests.Accept("receipt-least-privilege");
        var settings=new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(TestSettings.Configuration.Admin){
            InitialCatalog=TestSettings.Configuration.Database
        };
        Assert.StartsWith("tourlab_azure",settings.InitialCatalog);
        await using var connection=new Microsoft.Data.SqlClient.SqlConnection(settings.ConnectionString);
        await connection.OpenAsync();
        await using(var grant=connection.CreateCommand()){
            grant.CommandTimeout=5;
            grant.CommandText="""
                IF DATABASE_PRINCIPAL_ID(N'tourlab_azure_receipts') IS NULL CREATE USER [tourlab_azure_receipts] WITHOUT LOGIN WITH DEFAULT_SCHEMA=[notifications];
                IF IS_ROLEMEMBER(N'tourlab_azure_notifications_runtime',N'tourlab_azure_receipts')<>1
                    ALTER ROLE [tourlab_azure_notifications_runtime] ADD MEMBER [tourlab_azure_receipts];
                """+AzureSecurityPlan.ReceiptRestrictions;
            await grant.ExecuteNonQueryAsync();
            grant.CommandText="EXECUTE AS USER=N'tourlab_azure_receipts';";await grant.ExecuteNonQueryAsync();
        }
        await using(var db=new TourLab.Azure.Notifications.Persistence.NotificationsDb(
            new DbContextOptionsBuilder<TourLab.Azure.Notifications.Persistence.NotificationsDb>().UseSqlServer(connection,sql=>sql.CommandTimeout(5)).Options)){
            await new TourLab.Azure.Notifications.Receipts.ReceiptProcessor(db,ReceiptIntegrationTests.Storage(),TimeProvider.System).ProcessAsync(command,default);
            Assert.Equal("Ready",(await db.ReceiptJobs.SingleAsync(x=>x.Id==job.Id)).Status);
        }
        await using(var immutable=connection.CreateCommand()){
            immutable.CommandTimeout=5;
            immutable.CommandText="UPDATE notifications.ReceiptJobs SET SnapshotJson=SnapshotJson WHERE Id=@id";
            immutable.Parameters.AddWithValue("@id",job.Id);
            await Assert.ThrowsAsync<Microsoft.Data.SqlClient.SqlException>(()=>immutable.ExecuteNonQueryAsync());
        }
        foreach(var table in new[]{"notifications.Outbox","notifications.Notifications","notifications.ReceiptAttempts","identity.AspNetUsers","reservations.Reservations"}){
            await using var denied=connection.CreateCommand();denied.CommandTimeout=5;denied.CommandText="SELECT TOP(1) * FROM "+table;
            await Assert.ThrowsAsync<Microsoft.Data.SqlClient.SqlException>(()=>denied.ExecuteScalarAsync());
        }
    }
    private static ServiceProvider Services(string sql,ProtectionCertificates certificates)
    {
        var services=new ServiceCollection();services.AddLogging(l=>l.SetMinimumLevel(LogLevel.None));
        services.AddDbContext<IdentityDb>(o=>o.UseSqlServer(sql));
        services.AddDataProtection().SetApplicationName("TourLab.Azure.Gateway")
            .PersistKeysToDbContext<IdentityDb>().ProtectKeysWithCertificate(certificates.Active).UnprotectKeysWithAnyCertificate(certificates.All);
        return services.BuildServiceProvider();
    }
}
