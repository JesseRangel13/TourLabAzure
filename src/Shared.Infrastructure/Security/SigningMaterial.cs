using System.Security.Cryptography;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
namespace TourLab.Azure.Infrastructure.Security;

public static class SigningMaterial
{
    public static RSA Private(IConfiguration config,string section)
    {
        var rsa=RSA.Create();
        try {
            rsa.ImportFromPem(AzureSecurity.Cloud(config)?
                config[section+":PrivateKeyPem"]??throw new SecurityConfigurationException("SigningSecretMissing"):
                File.ReadAllText(config[section+":PrivateKeyPath"]??throw new SecurityConfigurationException("SigningFileMissing")));
            AzureSecurity.Require(rsa.KeySize>=3072&&rsa.ExportParameters(true).D is not null,"InvalidSigningMaterial");
            return rsa;
        } catch {rsa.Dispose();throw;}
    }
    public static string KeyId(RSA rsa)=>Convert.ToHexString(SHA256.HashData(rsa.ExportSubjectPublicKeyInfo()));
    public static SecurityKey[] Public(IConfiguration config,string section)
    {
        if(!AzureSecurity.Cloud(config)) {
            using var local=RSA.Create();local.ImportFromPem(File.ReadAllText(config[section+":PublicKeyPath"]??
                throw new SecurityConfigurationException("ValidationKeyMissing")));
            return [new RsaSecurityKey(local.ExportParameters(false))];
        }
        var items=config.GetSection(section+":ValidationKeys").Get<string[]>()??[];
        AzureSecurity.Require(items.Length is >=1 and <=2,"BoundedValidationKeysRequired");
        return items.Select(pem=>{
            AzureSecurity.Require(!pem.Contains("PRIVATE KEY",StringComparison.Ordinal),"PrivateValidationKey");
            using var rsa=RSA.Create();rsa.ImportFromPem(pem);
            AzureSecurity.Require(rsa.KeySize>=3072,"WeakValidationKey");
            return (SecurityKey)new RsaSecurityKey(rsa.ExportParameters(false)){KeyId=KeyId(rsa)};
        }).ToArray();
    }
}