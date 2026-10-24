using System.Net.Http.Json;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using TourLab.Azure.Infrastructure.Security;
using TourLab.Azure.Reservations;

namespace TourLab.Azure.Tests;

public sealed class BackendJwtTests
{
    [Theory]
    [InlineData("catalog")]
    [InlineData("reservations")]
    [InlineData("payments")]
    [InlineData("notifications")]
    public async Task EveryBackendRejectsInvalidTokensAndEnforcesRoleAndClaimIdentity(string service)
    {
        await using var factory = Factory(service);
        using var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost:9444"), AllowAutoRedirect = false });
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        using (var response = await client.GetAsync("/v1/status", timeout.Token)) Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        foreach (var fault in new[] { "Signature", "Issuer", "Audience", "Expired", "Unsigned", "NoSub", "DuplicateSub", "NoRole", "DuplicateRole", "InvalidRole", "TooLong", "Future" })
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "/v1/status");
            request.Headers.Authorization = new("Bearer", Token(service, "Alice", "Tourist", fault));
            using var response = await client.SendAsync(request, timeout.Token);
            Assert.True(response.StatusCode == HttpStatusCode.Unauthorized, $"Backend {service} accepted/reclassified invalid token type {fault} as {(int)response.StatusCode}.");
        }
        foreach (var user in new[] { "Alice", "Bob" })
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "/v1/status?userId=forged");
            request.Headers.Add("X-User-Id", "forged");
            request.Headers.Authorization = new("Bearer", Token(service, user, "Tourist"));
            using var response = await client.SendAsync(request, timeout.Token);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var body = await response.Content.ReadFromJsonAsync<JsonElement>(timeout.Token);
            Assert.Equal(user, body.GetProperty("userId").GetString());
        }
        client.DefaultRequestHeaders.Authorization = new("Bearer", Token(service, "Alice", "Tourist"));
        using (var forbidden = await client.GetAsync("/v1/admin/status", timeout.Token)) Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        client.DefaultRequestHeaders.Authorization = new("Bearer", Token(service, "Admin", "Admin"));
        using (var allowed = await client.GetAsync("/v1/admin/status", timeout.Token)) Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
    }

    [Fact]
    public async Task QuoteRouteRequiresSeparateServiceSignerAndCannotBePublishedByGateway()
    {
        await using var catalog = new AzureFactory<TourLab.Azure.Catalog.Program>("catalog");
        using var client = catalog.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", Token("catalog", "Alice", "Admin"));
        using (var human = await client.PostAsync("/v1/internal/quotes", null)) Assert.Equal(HttpStatusCode.Unauthorized, human.StatusCode);
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Quote:PrivateKeyPath"] = Path.Combine(TestSettings.Output, "quote-signing.key")
        }).Build();
        using var issuer = new QuoteServiceTokens(config, TimeProvider.System);
        client.DefaultRequestHeaders.Authorization = new("Bearer", issuer.Issue());
        using (var service = await client.PostAsync("/v1/internal/quotes", null)) Assert.Equal(HttpStatusCode.ServiceUnavailable, service.StatusCode);
        using (var humanRoute = await client.GetAsync("/v1/status")) Assert.Equal(HttpStatusCode.Unauthorized, humanRoute.StatusCode);
        await using var gateway = new AzureFactory<TourLab.Azure.Gateway.Program>("identity");
        using var browser = gateway.CreateClient();
        foreach (var path in new[] { "/api/catalog/v1/internal/quotes", "/api/catalog/v1/INTERNAL/quotes", "/api/catalog/v1/%69nternal/quotes" })
        {
            using var hidden = await browser.PostAsync(path, null);
            Assert.Equal(HttpStatusCode.NotFound, hidden.StatusCode);
        }
    }

    private static FactoryAdapter Factory(string service) => new(service);

    private sealed class FactoryAdapter : IAsyncDisposable
    {
        private readonly object factory;
        public FactoryAdapter(string service) => factory = service switch
        {
            "catalog" => new AzureFactory<TourLab.Azure.Catalog.Program>("catalog"),
            "reservations" => new AzureFactory<TourLab.Azure.Reservations.Program>("reservations"),
            "payments" => new AzureFactory<TourLab.Azure.Payments.Program>("payments"),
            _ => new AzureFactory<TourLab.Azure.Notifications.Program>("notifications")
        };
        public HttpClient CreateClient(Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions options) => factory switch
        {
            AzureFactory<TourLab.Azure.Catalog.Program> f => f.CreateClient(options),
            AzureFactory<TourLab.Azure.Reservations.Program> f => f.CreateClient(options),
            AzureFactory<TourLab.Azure.Payments.Program> f => f.CreateClient(options),
            AzureFactory<TourLab.Azure.Notifications.Program> f => f.CreateClient(options),
            _ => throw new InvalidOperationException()
        };
        public ValueTask DisposeAsync() => ((IAsyncDisposable)factory).DisposeAsync();
    }

    private static string Token(string service, string user, string role, string? fault = null)
    {
        using var key = RSA.Create(3072);
        if (fault != "Signature") key.ImportFromPem(File.ReadAllText(Path.Combine(TestSettings.Output, "human-signing.key")));
        var claims = new List<Claim> { new("name", user), new("jti", Guid.NewGuid().ToString()) };
        if (fault != "NoSub") claims.Add(new("sub", user));
        if (fault == "DuplicateSub") claims.Add(new("sub", "other"));
        if (fault != "NoRole") claims.Add(new("role", fault == "InvalidRole" ? "Root" : role));
        if (fault == "DuplicateRole") claims.Add(new("role", "Admin"));
        var now = DateTime.UtcNow;
        var start = fault == "Expired" ? now.AddMinutes(-10) : fault == "Future" ? now.AddMinutes(5) : now.AddSeconds(-1);
        var end = fault == "Expired" ? now.AddMinutes(-5) : fault == "TooLong" ? now.AddHours(1) : start.AddMinutes(2);
        var token = new JwtSecurityToken(fault == "Issuer" ? "foreign" : BackendSecurity.Issuer,
            fault == "Audience" ? "foreign" : BackendSecurity.Audience(service), claims, start, end,
            fault == "Unsigned" ? null : new SigningCredentials(new RsaSecurityKey(key) { CryptoProviderFactory = new CryptoProviderFactory { CacheSignatureProviders = false } }, SecurityAlgorithms.RsaSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
