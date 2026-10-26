using System.Text.RegularExpressions;
using Azure.Core;
using Azure.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using TourLab.Azure.Infrastructure.Persistence;
namespace TourLab.Azure.Infrastructure.Security;

public sealed class SecurityConfigurationException(string code) : Exception(code);
public static partial class AzureSecurity
{
    public static readonly string[] Workloads = ["gateway","catalog","reservations","payments","notifications","receipts"];
    public static bool Cloud(IConfiguration c) => c["AzureSecurity:Mode"] == "ManagedIdentity";
    public static TokenCredential Credential() => new ManagedIdentityCredential(ManagedIdentityId.SystemAssigned);
    public static global::Azure.Core.Cryptography.IKeyEncryptionKeyResolver KeyResolver()
    {
        var options=new global::Azure.Security.KeyVault.Keys.Cryptography.CryptographyClientOptions();
        options.Retry.MaxRetries=0;options.Retry.NetworkTimeout=TimeSpan.FromSeconds(5);
        options.Diagnostics.IsLoggingContentEnabled=false;
        return new global::Azure.Security.KeyVault.Keys.Cryptography.KeyResolver(Credential(),options);
    }
    public static string Schema(string workload) => workload switch {
        "gateway" => "identity", "receipts" => "notifications",
        "catalog" or "reservations" or "payments" or "notifications" => workload,
        _ => throw new SecurityConfigurationException("UnknownWorkload")
    };
    public static bool Endpoint(string? value, string suffix, string path = "/")
    {
        return Uri.TryCreate(value,UriKind.Absolute,out var uri) && uri.Scheme=="https" && uri.IsDefaultPort &&
            Regex.IsMatch(uri.Host,"^tourlabazure[a-z0-9-]*"+(suffix=="azurewebsites.net"?"(?:\\.[a-z0-9]+-[0-9]{2})?":"")+"\\."+Regex.Escape(suffix)+"$",RegexOptions.CultureInvariant) &&
            uri.AbsolutePath==path && string.IsNullOrEmpty(uri.Query) && string.IsNullOrEmpty(uri.Fragment) && string.IsNullOrEmpty(uri.UserInfo);
    }
    public static bool Namespace(string? value) => value is not null &&
        Regex.IsMatch(value,"^tourlabazure[a-z0-9-]*\\.servicebus\\.windows\\.net$",RegexOptions.CultureInvariant);
    public static bool ProtectionKey(string? value,string name) => Endpoint(value,"vault.azure.net","/keys/"+name);
    public static void Require(bool condition,string code) { if(!condition)throw new SecurityConfigurationException(code); }
    public static bool Validate(IConfiguration c, bool development, string workload)
    {
        Require(Workloads.Contains(workload,StringComparer.Ordinal),"UnknownWorkload");
        var mode=c["AzureSecurity:Mode"] ?? (development?"Local":"");
        if(mode=="Local") {
            Require(development,"LocalSecurityRequiresDevelopment");
            // No credentials or network clients are constructed for the local secret path.
            Require(string.IsNullOrEmpty(c["KeyVault:VaultUri"]),"LocalVaultNotSupported");
            return false;
        }
        Require(mode=="ManagedIdentity"&&!development,"ExplicitCloudModeRequired");
        Require(!string.Equals(Environment.GetEnvironmentVariable("ASPNETCORE_FORWARDEDHEADERS_ENABLED"),"true",StringComparison.OrdinalIgnoreCase) && !c.GetValue<bool>("ASPNETCORE_FORWARDEDHEADERS_ENABLED") && !c.GetValue<bool>("FORWARDEDHEADERS_ENABLED"),"AutomaticProxyTrustForbidden");
        Require(string.IsNullOrEmpty(c["AzureSecurity:ClientId"]),"SystemAssignedIdentityRequired");
        foreach(var key in new[]{"Tls:CaPath","Jwt:PrivateKeyPath","Jwt:PrivateKeyPem","Jwt:PublicKeyPath",
            "Quote:PrivateKeyPath","Quote:PrivateKeyPem","Quote:PublicKeyPath","DataProtection:CertificatePath",
            "DataProtection:CertificatePassword","Dashboard:CertificatePath","Dashboard:CertificatePassword",
            "Dashboard:KeyDirectory","Dashboard:Key","Dashboard:CaPath","ImageStorage:ConnectionString","ReceiptStorage:ConnectionString"})
            Require(string.IsNullOrEmpty(c[key]),"LocalOrStaticCredentialInCloud");
        Require(!c.GetSection("DataProtection:PreviousCertificates").GetChildren().Any(),"LocalCertificateInCloud");
        Require(!c.GetValue<bool>("Messaging:LocalTestBroker") && !c.GetValue<bool>("ReceiptTestBroker"),"TestBrokerInCloud");
        Require(!c.GetValue<bool>("LabFeaturesEnabled"),"DevelopmentFaultsInCloud");
        Require(!c.GetSection("ReceiptTestBarrier").GetChildren().Any(),"TestBarrierInCloud");
        Require(SqlRules.RuntimeSafe(new() {Schema=Schema(workload),ConnectionString=c.GetConnectionString("Runtime")??"",
            ManagedIdentity=true,Development=false}),"UnsafeCloudSql");
        if(workload!="receipts") {
            Require(Endpoint("https://"+c["AllowedHosts"]+"/","azurewebsites.net"),"ExplicitCloudHostRequired");
            _=CloudIngress.Options(c);
        }
        if(workload!="gateway"&&workload!="receipts") {
            Require(c.GetValue<bool>("Messaging:Enabled") && string.IsNullOrEmpty(c["Messaging:ConnectionString"]) &&
                Namespace(c["Messaging:FullyQualifiedNamespace"]),"UnsafeCloudMessaging");
        }
        if(workload=="gateway") {
            Require(Endpoint(c["KeyVault:VaultUri"],"vault.azure.net"),"UnsafeVault");
            Require(ProtectionKey(c["DataProtection:KeyIdentifier"],"tourlab-azure-gateway-protection") &&
                new Uri(c["DataProtection:KeyIdentifier"]!).Host==new Uri(c["KeyVault:VaultUri"]!).Host,"UnsafeGatewayProtection");
            var origins=c.GetSection("Browser:AllowedOrigins").Get<string[]>()??[];
            Require(origins.Length==1 && origins[0]=="https://"+c["AllowedHosts"],"ExplicitSameOriginRequired");
            foreach(var service in new[]{"catalog","reservations","payments","notifications"})
                Require(Endpoint(c["Backends:"+service],"azurewebsites.net"),"UnsafeBackendAddress");
        }
        if(workload=="reservations") {
            Require(Endpoint(c["KeyVault:VaultUri"],"vault.azure.net"),"UnsafeVault");
            Require(Endpoint(c["Catalog:BaseAddress"],"azurewebsites.net"),"UnsafeCatalogAddress");
            Require(c.GetValue<bool>("Dashboard:Enabled")&&!c.GetValue<bool>("Dashboard:Local") &&
                Endpoint(c["Dashboard:Endpoint"],"documents.azure.com") &&
                c["Dashboard:Database"]=="tourlab-azure-dashboard"&&c["Dashboard:Container"]=="bookings","UnsafeCloudCosmos");
            Require(Endpoint(c["Dashboard:KeyBlobUri"],"blob.core.windows.net","/tourlab-azure-dashboard-keys/cursor-ring.xml") &&
                ProtectionKey(c["Dashboard:ProtectionKeyIdentifier"],"tourlab-azure-dashboard-protection") &&
                new Uri(c["Dashboard:ProtectionKeyIdentifier"]!).Host==new Uri(c["KeyVault:VaultUri"]!).Host,"UnsafeDashboardProtection");
        }
        if(workload=="catalog") Storage(c,"ImageStorage","tourlab-azure-tour-images");
        if(workload is "notifications" or "receipts") Storage(c,"ReceiptStorage","tourlab-azure-booking-receipts");
        if(workload=="receipts") {
            Require(string.IsNullOrEmpty(c["ReceiptBus"])&&Namespace(c["ReceiptBus:fullyQualifiedNamespace"]) &&
                c["ReceiptBus:credential"]=="managedidentity"&&string.IsNullOrEmpty(c["ReceiptBus:clientId"]) &&
                string.IsNullOrEmpty(c["ReceiptBus:managedIdentityResourceId"]),"UnsafeFunctionBus");
            Require(string.IsNullOrEmpty(c["AzureWebJobsStorage"]) && c["AzureWebJobsStorage:credential"]=="managedidentity" &&
                string.IsNullOrEmpty(c["AzureWebJobsStorage:clientId"])&&string.IsNullOrEmpty(c["AzureWebJobsStorage:managedIdentityResourceId"]) &&
                Endpoint(c["AzureWebJobsStorage:blobServiceUri"],"blob.core.windows.net") &&
                Endpoint(c["AzureWebJobsStorage:queueServiceUri"],"queue.core.windows.net") &&
                Endpoint(c["AzureWebJobsStorage:tableServiceUri"],"table.core.windows.net"),"UnsafeFunctionHostStorage");
            Require(new Uri(c["AzureWebJobsStorage:blobServiceUri"]!).Host!=new Uri(c["ReceiptStorage:ServiceUri"]!).Host,"SeparateFunctionHostAccountRequired");
        }
        return true;
    }
    private static void Storage(IConfiguration c,string section,string container) => Require(
        c.GetValue<bool>(section+":Enabled")&&!c.GetValue<bool>(section+":Local") &&
        Endpoint(c[section+":ServiceUri"],"blob.core.windows.net") && c[section+":Container"]==container,"UnsafeCloudStorage");
}
