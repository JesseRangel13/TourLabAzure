using System.Security.Cryptography.X509Certificates;
using System.Net.Http.Headers;
using System.Security.Claims;
using Yarp.ReverseProxy.Configuration;
using Yarp.ReverseProxy.Transforms;
using TourLab.Azure.Gateway.Identity;

namespace TourLab.Azure.Gateway.Browser;

public static class ProxySetup
{
    public static void AddLabProxy(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        services.AddReverseProxy().LoadFromConfigSection()
            .ConfigureHttpClient((_, handler) =>
            {
                handler.MaxConnectionsPerServer = 10;
                handler.ConnectTimeout = TimeSpan.FromSeconds(3);
                handler.PooledConnectionLifetime = TimeSpan.FromMinutes(2);
                handler.AllowAutoRedirect = false;
                var caPath = configuration["Tls:CaPath"];
                if (!string.IsNullOrWhiteSpace(caPath))
                {
                    if (!environment.IsDevelopment()) throw new InvalidOperationException("Local lab CA override is Development-only.");
                    // Explicitly trust the lab CA while preserving normal TLS name/signature/expiry validation.
                    handler.SslOptions.CertificateChainPolicy = new X509ChainPolicy
                    {
                        TrustMode = X509ChainTrustMode.CustomRootTrust,
                        RevocationMode = X509RevocationMode.NoCheck
                    };
                    handler.SslOptions.CertificateChainPolicy.CustomTrustStore.Add(X509CertificateLoader.LoadCertificateFromFile(caPath));
                }
            })
            .AddTransforms(context =>
            {
                var service = context.Route.ClusterId;
                context.AddRequestTransform(transform =>
                {
                    var tokens = transform.HttpContext.RequestServices.GetRequiredService<HumanTokens>();
                    var user = transform.HttpContext.User;
                    var bearer = tokens.Issue(user.FindFirstValue(ClaimTypes.NameIdentifier)!,
                        user.Identity!.Name!, user.FindFirstValue(ClaimTypes.Role)!, service!);
                    transform.ProxyRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
                    transform.ProxyRequest.Headers.Remove("Cookie");
                    transform.ProxyRequest.Headers.Remove("X-XSRF-TOKEN");
                    transform.ProxyRequest.Headers.Remove("X-AZURELAB-XSRF-TOKEN");
                    return ValueTask.CompletedTask;
                });
            });
    }

    // Resolves final configuration after WebApplicationFactory or private settings are applied.
    private static IReverseProxyBuilder LoadFromConfigSection(this IReverseProxyBuilder proxy)
    {
        proxy.Services.AddSingleton<IProxyConfigProvider, LabProxyConfig>();
        return proxy;
    }

    private sealed class LabProxyConfig(IConfiguration configuration) : IProxyConfigProvider
    {
        public IProxyConfig GetConfig()
        {
            var routes = new List<RouteConfig>();
            var clusters = new List<ClusterConfig>();
            foreach (var service in HumanTokens.Services)
            {
                var address = configuration[$"Backends:{service}"];
                if (!Uri.TryCreate(address, UriKind.Absolute, out var uri) || uri.Scheme != "https" ||
                    !string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(uri.Query))
                    throw new InvalidOperationException("Every backend requires an explicit HTTPS address.");
                routes.Add(new RouteConfig
                {
                    RouteId = service, ClusterId = service, AuthorizationPolicy = "Browser",
                    Match = new RouteMatch { Path = $"/api/{service}/{{**catch-all}}" },
                    Transforms = [new Dictionary<string, string> { ["PathRemovePrefix"] = $"/api/{service}" },
                        new Dictionary<string, string> { ["X-Forwarded"] = "Off" }]
                });
                clusters.Add(new ClusterConfig
                {
                    ClusterId = service,
                    Destinations = new Dictionary<string, DestinationConfig> { ["owned"] = new() { Address = uri.AbsoluteUri } },
                    HttpRequest = new Yarp.ReverseProxy.Forwarder.ForwarderRequestConfig { ActivityTimeout = TimeSpan.FromSeconds(10) }
                });
            }
            return new Config(routes, clusters);
        }
    }

    private sealed class Config(IReadOnlyList<RouteConfig> routes, IReadOnlyList<ClusterConfig> clusters) : IProxyConfig
    {
        public IReadOnlyList<RouteConfig> Routes => routes;
        public IReadOnlyList<ClusterConfig> Clusters => clusters;
        public Microsoft.Extensions.Primitives.IChangeToken ChangeToken => Microsoft.Extensions.FileProviders.NullChangeToken.Singleton;
    }
}
