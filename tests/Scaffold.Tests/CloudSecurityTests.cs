using System.Security.Cryptography;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using TourLab.Azure.Infrastructure.Security;
using TourLab.Azure.Infrastructure.Persistence;
using TourLab.Azure.Provisioning;
namespace TourLab.Azure.Tests;

public sealed class CloudSecurityTests
{
    internal static ConfigurationManager Config(string workload,string? publicPem=null,string? keyId=null)
    {
        using var generated=RSA.Create(3072);
        publicPem??=generated.ExportSubjectPublicKeyInfoPem();keyId??=SigningMaterial.KeyId(generated);
        var values=new Dictionary<string,string?> {
            ["AzureSecurity:Mode"]="ManagedIdentity",
            ["AllowedHosts"]="tourlabazure-"+workload+".azurewebsites.net",
            ["Browser:AllowedOrigins:0"]="https://tourlabazure-"+workload+".azurewebsites.net",
            ["Ingress:KnownProxies:0"]="10.10.0.4",
            ["ConnectionStrings:Runtime"]="Server=tcp:tourlabazure-study.database.windows.net,1433;Database=tourlab_azure_study;Authentication=Active Directory Managed Identity;Encrypt=Strict;TrustServerCertificate=False;Max Pool Size=5;Connect Timeout=5",
            ["Messaging:Enabled"]="true",["Messaging:FullyQualifiedNamespace"]="tourlabazure-study.servicebus.windows.net",
            ["Jwt:ValidationKeys:0"]=publicPem,["Quote:ValidationKeys:0"]=publicPem,
            ["KeyVault:VaultUri"]="https://tourlabazure-study.vault.azure.net/",
            ["KeyVault:ExpectedSigningKeyId"]=keyId,
            ["KeyVault:SigningSecretIdentifier"]="https://tourlabazure-study.vault.azure.net/secrets/"+(workload=="gateway"?"tourlab-azure-human-signing":"tourlab-azure-quote-signing")+"/11111111111111111111111111111111",
            ["DataProtection:KeyIdentifier"]="https://tourlabazure-study.vault.azure.net/keys/tourlab-azure-gateway-protection",
            ["Catalog:BaseAddress"]="https://tourlabazure-catalog.azurewebsites.net/",
            ["Dashboard:Enabled"]="true",["Dashboard:Local"]="false",["Dashboard:Endpoint"]="https://tourlabazure-study.documents.azure.com/",
            ["Dashboard:Database"]="tourlab-azure-dashboard",["Dashboard:Container"]="bookings",
            ["Dashboard:KeyBlobUri"]="https://tourlabazuredash.blob.core.windows.net/tourlab-azure-dashboard-keys/cursor-ring.xml",
            ["Dashboard:ProtectionKeyIdentifier"]="https://tourlabazure-study.vault.azure.net/keys/tourlab-azure-dashboard-protection",
            ["ImageStorage:Enabled"]="true",["ImageStorage:Local"]="false",["ImageStorage:ServiceUri"]="https://tourlabazureimages.blob.core.windows.net/",
            ["ImageStorage:Container"]="tourlab-azure-tour-images",
            ["ReceiptStorage:Enabled"]="true",["ReceiptStorage:Local"]="false",["ReceiptStorage:ServiceUri"]="https://tourlabazurereceipts.blob.core.windows.net/",
            ["ReceiptStorage:Container"]="tourlab-azure-booking-receipts",
            ["ReceiptBus:fullyQualifiedNamespace"]="tourlabazure-study.servicebus.windows.net",["ReceiptBus:credential"]="managedidentity",
            ["AzureWebJobsStorage:credential"]="managedidentity",
            ["AzureWebJobsStorage:blobServiceUri"]="https://tourlabazurehost.blob.core.windows.net/",
            ["AzureWebJobsStorage:queueServiceUri"]="https://tourlabazurehost.queue.core.windows.net/",
            ["AzureWebJobsStorage:tableServiceUri"]="https://tourlabazurehost.table.core.windows.net/"
        };
        foreach(var service in new[]{"catalog","reservations","payments","notifications"})
            values["Backends:"+service]="https://tourlabazure-"+service+".azurewebsites.net/";
        var config=new ConfigurationManager();config.AddInMemoryCollection(values);return config;
    }
    [Theory]
    [InlineData("gateway")][InlineData("catalog")][InlineData("reservations")][InlineData("payments")][InlineData("notifications")][InlineData("receipts")]
    public void EachCloudWorkloadValidatesOfflineWithoutAcquiringCredentials(string workload)
    {
        var reference=StartupSecrets.Validate(Config(workload),false,workload);
        Assert.Equal(workload is "gateway" or "reservations",reference is not null);
        Assert.IsType<global::Azure.Identity.ManagedIdentityCredential>(AzureSecurity.Credential());
    }
    [Theory]
    [InlineData("ASPNETCORE_FORWARDEDHEADERS_ENABLED","true")]
    [InlineData("Tls:CaPath","local-ca")]
    [InlineData("Jwt:PrivateKeyPath","private.key")]
    [InlineData("Jwt:PrivateKeyPem","inline-secret")]
    [InlineData("AzureSecurity:ClientId","developer-id")]
    [InlineData("Messaging:ConnectionString","Endpoint=sb://foreign")]
    [InlineData("LabFeaturesEnabled","true")]
    [InlineData("Messaging:LocalTestBroker","true")]
    [InlineData("ReceiptTestBroker","true")]
    [InlineData("Dashboard:Key","master-key")]
    [InlineData("ReceiptTestBarrier:JobId","test-only")]
    public void CloudRejectsStaticLocalOrDevelopmentSettingsBeforeSecretReader(string setting,string value)
    {
        var c=Config("reservations");c[setting]=value;
        Assert.Throws<SecurityConfigurationException>(()=>StartupSecrets.Validate(c,false,"reservations"));
    }
    [Theory]
    [InlineData("KeyVault:VaultUri","http://tourlabazure-study.vault.azure.net/")]
    [InlineData("KeyVault:VaultUri","https://tourlabazure-study.vault.azure.net.evil.test/")]
    [InlineData("KeyVault:VaultUri","https://tourlabazure-study.vault.azure.net:444/")]
    [InlineData("DataProtection:KeyIdentifier","https://tourlabazure-study.vault.azure.net/keys/tourlab-azure-gateway-protection/version")]
    [InlineData("KeyVault:SigningSecretIdentifier","https://tourlabazure-study.vault.azure.net/secrets/tourlab-azure-human-signing")]
    [InlineData("KeyVault:SigningSecretIdentifier","https://tourlabazure-study.vault.azure.net/secrets/foreign/11111111111111111111111111111111")]
    [InlineData("Backends:catalog","https://localhost:9444/")]
    public void EndpointScopeVersionAndTlsGuardsRejectUnsafeCloudPlans(string setting,string value)
    {
        var c=Config("gateway");c[setting]=value;
        Assert.Throws<SecurityConfigurationException>(()=>StartupSecrets.Validate(c,false,"gateway"));
    }
    [Theory]
    [InlineData("AzureWebJobsStorage","UseDevelopmentStorage=true")]
    [InlineData("AzureWebJobsStorage:credential","default")]
    [InlineData("AzureWebJobsStorage:clientId","foreign-identity")]
    [InlineData("AzureWebJobsStorage:blobServiceUri","https://tourlabazurereceipts.blob.core.windows.net/")]
    [InlineData("ReceiptBus","Endpoint=sb://localhost:5673")]
    [InlineData("ReceiptBus:clientId","foreign-identity")]
    public void FunctionHostAndTriggerIdentityCannotSilentlyFallBackOrUseBusinessAccount(string setting,string value)
    {
        var c=Config("receipts");c[setting]=value;
        Assert.Throws<SecurityConfigurationException>(()=>StartupSecrets.Validate(c,false,"receipts"));
    }
    [Fact]
    public async Task LocalBootstrapDoesNotConstructOrContactKeyVaultAndProductionCannotImplicitlyUseLocal()
    {
        var local=new ConfigurationManager();var reader=new Reader((_,_,_,_)=>throw new InvalidOperationException("Must not execute"));
        await StartupSecrets.LoadAsync(local,true,"gateway",default,reader);Assert.Equal(0,reader.Calls);
        Assert.Throws<SecurityConfigurationException>(()=>StartupSecrets.Validate(local,false,"gateway"));
    }
    [Fact]
    public async Task OnlyExactVersionedSigningSecretLoadsOnceIntoMemoryAndMismatchFailsClosed()
    {
        using var rsa=RSA.Create(3072);var c=Config("gateway",rsa.ExportSubjectPublicKeyInfoPem(),SigningMaterial.KeyId(rsa));
        var reader=new Reader((vault,name,version,ct)=>{
            Assert.Equal("tourlabazure-study.vault.azure.net",vault.Host);Assert.Equal("tourlab-azure-human-signing",name);
            Assert.Equal(new string('1',32),version);Assert.True(ct.CanBeCanceled);return Task.FromResult(rsa.ExportPkcs8PrivateKeyPem());
        });
        await StartupSecrets.LoadAsync(c,false,"gateway",default,reader);Assert.Equal(1,reader.Calls);
        using var loaded=SigningMaterial.Private(c,"Jwt");Assert.Equal(SigningMaterial.KeyId(rsa),SigningMaterial.KeyId(loaded));
        var mismatch=Config("gateway");var error=await Assert.ThrowsAsync<SecurityConfigurationException>(()=>
            StartupSecrets.LoadAsync(mismatch,false,"gateway",default,reader));
        Assert.Equal("SigningSecretUnavailable",error.Message);Assert.Null(mismatch["Jwt:PrivateKeyPem"]);
    }
    [Fact]
    public async Task SecretDeniedOrCancelledDoesNotExposeResponseBodyOrUseAFileFallback()
    {
        var sentinel="PRIVATE-TEST-VALUE-DO-NOT-LOG";
        var c=Config("gateway");
        var denied=new Reader((_,_,_,_)=>throw new global::Azure.RequestFailedException(403,sentinel));
        var error=await Assert.ThrowsAsync<SecurityConfigurationException>(()=>StartupSecrets.LoadAsync(c,false,"gateway",default,denied));
        Assert.Equal("SigningSecretUnavailable",error.Message);Assert.Null(error.InnerException);Assert.DoesNotContain(sentinel,error.ToString());
        Assert.Null(c["Jwt:PrivateKeyPem"]);Assert.Equal(1,denied.Calls);
        using var cancelled=new CancellationTokenSource();cancelled.Cancel();
        var reader=new Reader((_,_,_,ct)=>{ct.ThrowIfCancellationRequested();return Task.FromResult("unreachable");});
        await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>StartupSecrets.LoadAsync(c,false,"gateway",cancelled.Token,reader));
    }
    [Fact]
    public void AzureSqlRequiresOnlySystemManagedIdentityAndStrictVerifiedTls()
    {
        var source=Config("catalog").GetConnectionString("Runtime")!;
        Assert.True(SqlRules.RuntimeSafe(new(){Schema="catalog",ConnectionString=source,ManagedIdentity=true}));
        foreach(var change in new Dictionary<string,string>{
            ["Authentication"]="Active Directory Default",["User ID"]=Guid.NewGuid().ToString(),["Password"]="static",
            ["Encrypt"]="Mandatory",["TrustServerCertificate"]="true",["Server"]="localhost,1433",
            ["Initial Catalog"]="master",["Max Pool Size"]="100",["HostNameInCertificate"]="*.windows.net",["ServerCertificate"]="local.crt"}) {
            var cs=new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(source){[change.Key]=change.Value};
            Assert.False(SqlRules.RuntimeSafe(new(){Schema="catalog",ConnectionString=cs.ConnectionString,ManagedIdentity=true}));
        }
    }
    [Fact]
    public void CloudJwtRotationAcceptsOnlyTwoDeclaredPublicKeysAndPreservesIssuerAudienceLifetimeAndKid()
    {
        using var old=RSA.Create(3072);using var next=RSA.Create(3072);using var foreign=RSA.Create(3072);
        var c=Config("catalog",next.ExportSubjectPublicKeyInfoPem());
        c["Jwt:ValidationKeys:1"]=old.ExportSubjectPublicKeyInfoPem();
        var parameters=BackendSecurity.Parameters(c,"Jwt",BackendSecurity.Issuer,BackendSecurity.Audience("Catalog"));
        var handler=new JwtSecurityTokenHandler();
        string Token(RSA key,string? kid=null,string? audience=null,DateTime? expiry=null){
            var now=DateTime.UtcNow.AddSeconds(-1);
            return handler.WriteToken(new JwtSecurityToken(BackendSecurity.Issuer,audience??BackendSecurity.Audience("Catalog"),
                [new Claim("sub","owner"),new Claim("role","Tourist")],now,expiry??now.AddMinutes(2),
                new SigningCredentials(new RsaSecurityKey(key){KeyId=kid??SigningMaterial.KeyId(key)},SecurityAlgorithms.RsaSha256)));
        }
        handler.ValidateToken(Token(old),parameters,out _);handler.ValidateToken(Token(next),parameters,out _);
        Assert.ThrowsAny<SecurityTokenException>(()=>handler.ValidateToken(Token(foreign),parameters,out _));
        Assert.ThrowsAny<SecurityTokenException>(()=>handler.ValidateToken(Token(next,"wrong-kid"),parameters,out _));
        Assert.ThrowsAny<SecurityTokenException>(()=>handler.ValidateToken(Token(next,audience:"foreign"),parameters,out _));
        c["Jwt:ValidationKeys:2"]=foreign.ExportSubjectPublicKeyInfoPem();Assert.Throws<SecurityConfigurationException>(()=>SigningMaterial.Public(c,"Jwt"));
        c["Jwt:ValidationKeys:2"]=null;c["Jwt:ValidationKeys:0"]=next.ExportPkcs8PrivateKeyPem();
        Assert.Throws<SecurityConfigurationException>(()=>SigningMaterial.Public(c,"Jwt"));
    }
    [Fact]
    public void SqlArtifactUsesDistinctBoundObjectIdsOwnedRolesAndNoServerLoginsOrPasswords()
    {
        var plan=new AzurePrincipalPlan("tourlab_azure_review",SqlRules.Schemas.ToDictionary(s=>s,_=>Guid.NewGuid()),
            SqlRules.Schemas.ToDictionary(s=>s,_=>Guid.NewGuid()),Guid.NewGuid());
        var sql=AzureSecurityPlan.Sql(plan);
        Assert.Contains("TYPE=E",sql);Assert.Contains("Unexpected inherited runtime role",sql);Assert.Contains("tourlab_azure_receipts",sql);
        Assert.DoesNotContain("CREATE LOGIN",sql);Assert.DoesNotContain("PASSWORD",sql);Assert.DoesNotContain("db_owner",sql);
        Assert.Contains("Unexpected inherited migrator role",sql);Assert.Contains("TourLabAzure.Project",sql);Assert.Contains("__EFMigrationsHistory",sql);
        Assert.Throws<ArgumentException>(()=>AzureSecurityPlan.Sql(plan with{Database="master'; DROP DATABASE x"}));
        plan.Runtime["catalog"]=plan.Runtime["identity"];Assert.Throws<ArgumentException>(()=>AzureSecurityPlan.Sql(plan));
    }
    internal sealed class Reader(Func<Uri,string,string,CancellationToken,Task<string>> read):IStartupSecretReader
    {
        public int Calls;
        public Task<string> ReadAsync(Uri vault,string name,string version,CancellationToken ct){Calls++;return read(vault,name,version,ct);}
    }
}
