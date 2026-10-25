using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Configuration;
namespace TourLab.Azure.Infrastructure.Security;

public sealed class ProtectionCertificates : IDisposable
{
    public X509Certificate2 Active { get; }
    public X509Certificate2[] All { get; }
    private ProtectionCertificates(X509Certificate2 active,X509Certificate2[] all){Active=active;All=all;}
    public static ProtectionCertificates Load(IConfiguration config,bool development)
    {
        AzureSecurity.Require(development,"LocalCertificateRequiresDevelopment");
        var certificates=new List<X509Certificate2>();
        try {
            var active=Read(config["CertificatePath"],config["CertificatePassword"]);
            certificates.Add(active);
            AzureSecurity.Require(active.NotBefore.ToUniversalTime()<=DateTime.UtcNow && active.NotAfter.ToUniversalTime()>DateTime.UtcNow,"ExpiredActiveProtectionCertificate");
            var previous=config.GetSection("PreviousCertificates").GetChildren().ToArray();
            AzureSecurity.Require(previous.Length<=2,"TooManyPreviousCertificates");
            foreach(var item in previous)certificates.Add(Read(item["Path"],item["Password"]));
            return new(active,certificates.ToArray());
        } catch {foreach(var certificate in certificates)certificate.Dispose();throw;}
    }
    private static X509Certificate2 Read(string? path,string? password)
    {
        if(string.IsNullOrWhiteSpace(path))throw new SecurityConfigurationException("ProtectionCertificateRequired");
        var certificate=X509CertificateLoader.LoadPkcs12FromFile(path,password,X509KeyStorageFlags.EphemeralKeySet);
        if(!certificate.HasPrivateKey){certificate.Dispose();throw new SecurityConfigurationException("PrivateProtectionCertificateRequired");}
        using var key=certificate.GetRSAPrivateKey();
        if(key is null||key.KeySize<3072){certificate.Dispose();throw new SecurityConfigurationException("StrongRsaProtectionCertificateRequired");}
        return certificate;
    }
    public void Dispose(){foreach(var certificate in All)certificate.Dispose();}
}