using System.Security.Cryptography.X509Certificates;
namespace TourLab.Azure.Reservations.Application;
public static class QuoteHttpSetup
{
    public static SocketsHttpHandler Handler(IConfiguration configuration, IHostEnvironment environment)
    {
        var handler=new SocketsHttpHandler {
            UseCookies=false,AllowAutoRedirect=false,MaxConnectionsPerServer=10,
            ConnectTimeout=TimeSpan.FromSeconds(3),PooledConnectionLifetime=TimeSpan.FromMinutes(2)
        };
        var path=configuration["Tls:CaPath"];
        if(environment.IsDevelopment()&&!string.IsNullOrWhiteSpace(path))
        {
            handler.SslOptions.CertificateChainPolicy=new X509ChainPolicy {
                TrustMode=X509ChainTrustMode.CustomRootTrust,RevocationMode=X509RevocationMode.NoCheck
            };
            handler.SslOptions.CertificateChainPolicy.CustomTrustStore.Add(X509CertificateLoader.LoadCertificateFromFile(path));
        }
        return handler;
    }
}