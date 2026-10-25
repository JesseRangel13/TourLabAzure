using Azure.Security.KeyVault.Secrets;
using Azure;
using Microsoft.Extensions.Configuration;
namespace TourLab.Azure.Infrastructure.Security;

// Replaceable boundary for unit tests; actual production path uses SecretClient with only system MI.
public interface IStartupSecretReader
{
    Task<string> ReadAsync(Uri vault,string name,string version,CancellationToken ct);
}
public sealed class VaultSecretReader : IStartupSecretReader
{
    private readonly SecretClient? supplied;
    public VaultSecretReader(SecretClient? supplied=null) => this.supplied=supplied;
    public async Task<string> ReadAsync(Uri vault,string name,string version,CancellationToken ct)
    {
        var options=new SecretClientOptions();
        options.Retry.MaxRetries=0; options.Retry.NetworkTimeout=TimeSpan.FromSeconds(5);
        options.Diagnostics.IsLoggingContentEnabled=false;
        var client=supplied??new SecretClient(vault,AzureSecurity.Credential(),options);
        var secret=(await client.GetSecretAsync(name,version,ct)).Value;
        AzureSecurity.Require(secret.Properties.Enabled!=false &&
            (secret.Properties.ExpiresOn is null||secret.Properties.ExpiresOn>DateTimeOffset.UtcNow) &&
            (secret.Properties.NotBefore is null||secret.Properties.NotBefore<=DateTimeOffset.UtcNow) &&
            secret.Value.Length is >0 and <=16384,"InvalidSigningSecret");
        return secret.Value;
    }
}
public sealed record SigningSecretReference(Uri Vault,string Name,string Version,string Section);
public static class StartupSecrets
{
    public static SigningSecretReference? Validate(IConfiguration config,bool development,string workload)
    {
        if(!AzureSecurity.Validate(config,development,workload))return null;
        if(workload is not ("gateway" or "receipts")) _=SigningMaterial.Public(config,"Jwt");
        if(workload=="catalog") _=SigningMaterial.Public(config,"Quote");
        // Validate ALL non-secret configuration before any Key Vault call.
        if(workload is not ("gateway" or "reservations"))return null;
        var name=workload=="gateway"?"tourlab-azure-human-signing":"tourlab-azure-quote-signing";
        var key=workload=="gateway"?"Jwt":"Quote";
        AzureSecurity.Require(System.Text.RegularExpressions.Regex.IsMatch(config["KeyVault:ExpectedSigningKeyId"]??"", "^[A-F0-9]{64}$"),"ExpectedSigningKeyIdRequired");
        var vault=new Uri(config["KeyVault:VaultUri"]!);
        var uri=config["KeyVault:SigningSecretIdentifier"];
        AzureSecurity.Require(Uri.TryCreate(uri,UriKind.Absolute,out var secret)&&
            AzureSecurity.Endpoint(vault.AbsoluteUri,"vault.azure.net")&&secret.Host==vault.Host&&
            secret.Scheme=="https"&&secret.IsDefaultPort&&string.IsNullOrEmpty(secret.Query)&&
            string.IsNullOrEmpty(secret.Fragment)&&string.IsNullOrEmpty(secret.UserInfo)&&
            System.Text.RegularExpressions.Regex.IsMatch(secret.AbsolutePath,"^/secrets/"+name+"/[a-f0-9]{32}$"),"VersionedSigningSecretRequired");
        return new(vault,name,secret!.Segments[^1],key);
    }
    public static async Task LoadAsync(ConfigurationManager config,bool development,string workload,CancellationToken ct,
        IStartupSecretReader? reader=null)
    {
        var reference=Validate(config,development,workload);
        if(reference is null)return;
        using var budget=CancellationTokenSource.CreateLinkedTokenSource(ct);budget.CancelAfter(TimeSpan.FromSeconds(20));
        try {
            var value=await (reader??new VaultSecretReader()).ReadAsync(reference.Vault,reference.Name,reference.Version,budget.Token);
            using var rsa=System.Security.Cryptography.RSA.Create();
            rsa.ImportFromPem(value);
            AzureSecurity.Require(rsa.KeySize>=3072&&rsa.ExportParameters(true).D is not null,"InvalidSigningSecret");
            AzureSecurity.Require(SigningMaterial.KeyId(rsa)==config["KeyVault:ExpectedSigningKeyId"],"SigningKeyVersionMismatch");
            config.AddInMemoryCollection(new Dictionary<string,string?>{[reference.Section+":PrivateKeyPem"]=value});
        }
        catch(OperationCanceledException) when(ct.IsCancellationRequested){throw;}
        catch(Exception error) when(error is RequestFailedException or global::Azure.Identity.AuthenticationFailedException or
            global::Azure.Identity.CredentialUnavailableException or System.Security.Cryptography.CryptographicException or ArgumentException or
            SecurityConfigurationException or OperationCanceledException) {
            // Never log vault response bodies, secret values, SDK exception text or inner exceptions.
            throw new SecurityConfigurationException("SigningSecretUnavailable");
        }
    }
}