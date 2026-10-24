using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using TourLab.Azure.Gateway.Identity;

namespace TourLab.Azure.Tests;

public sealed class LiveFactAttribute : FactAttribute
{
    public LiveFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("AZURELAB_LIVE_BFF") != "1")
            Skip = "NOT VERIFIED: use Verify-HttpsBff.ps1 to run real HTTPS process tests.";
    }
}

[Collection("SQL Identity")]
public sealed class LiveBffTests
{
    private static readonly Uri Origin = new("https://localhost:9443");

    [LiveFact]
    public async Task RoundTripUsesCookieIdentityAndPerBackendTokenWithoutForwardingClientBearer()
    {
        using var alice = Client();
        using var bob = Client(captureBob: true);
        using var admin = Client();
        await Login(alice, "Alice");
        await Login(bob, "Bob");
        await Login(admin, "Admin");
        var aliceId = (await alice.GetFromJsonAsync<CurrentUser>("/auth/me"))!.UserId;
        var bobId = (await bob.GetFromJsonAsync<CurrentUser>("/auth/me"))!.UserId;
        Assert.NotEqual(aliceId, bobId);
        foreach (var service in new[] { "catalog", "reservations", "payments", "notifications" })
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/{service}/v1/status?userId={bobId}");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "client-forgery");
            request.Headers.Add("X-User-Id", bobId);
            using var response = await alice.SendAsync(request);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var body = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal(aliceId, body.GetProperty("userId").GetString());
            using var denied = await alice.GetAsync($"/api/{service}/v1/admin/status");
            Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
            using var allowed = await admin.GetAsync($"/api/{service}/v1/admin/status");
            Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
        }
        using (var missingCsrf = await alice.PostAsJsonAsync("/api/catalog/v1/admin/tours", new { name = "Forbidden" }))
            Assert.Equal(HttpStatusCode.BadRequest, missingCsrf.StatusCode);
        var bobState = bobHandler!.CookieContainer.GetCookies(Origin)["__Host-tourlab-azure"]!;
        await File.WriteAllTextAsync(Path.Combine(TestSettings.Output, "live-bob.cookie"), bobState.Value);
        var csrf = await Csrf(alice);
        using var logout = new HttpRequestMessage(HttpMethod.Post, "/auth/logout") { Content = JsonContent.Create(new { }) };
        logout.Headers.Add("X-AZURELAB-XSRF-TOKEN", csrf);
        using (var response = await alice.SendAsync(logout)) Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        using (var expired = await alice.GetAsync("/auth/me")) Assert.Equal(HttpStatusCode.Unauthorized, expired.StatusCode);
        Assert.Equal("Bob", (await bob.GetFromJsonAsync<CurrentUser>("/auth/me"))!.UserName);
    }

    [LiveFact]
    public async Task RestartedGatewayAcceptsExistingCookieAndTlsRejectsAnUntrustedRoot()
    {
        var cookie = await File.ReadAllTextAsync(Path.Combine(TestSettings.Output, "live-bob.cookie"));
        using var client = Client();
        client.DefaultRequestHeaders.Add("Cookie", "__Host-tourlab-azure=" + cookie);
        using var response = await client.GetAsync("/auth/me");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Bob", (await response.Content.ReadFromJsonAsync<CurrentUser>())!.UserName);
        using var key = RSA.Create(3072);
        var request = new CertificateRequest("CN=Wrong Lab Root", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
        using var wrongRoot = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddHours(1));
        using var wrong = Client(wrongRoot);
        await Assert.ThrowsAsync<HttpRequestException>(() => wrong.GetAsync("/health/live"));
    }

    private static SocketsHttpHandler? bobHandler;
    private static HttpClient Client(X509Certificate2? trust = null, bool captureBob = false)
    {
        var handler = new SocketsHttpHandler { UseCookies = true, AllowAutoRedirect = false, ConnectTimeout = TimeSpan.FromSeconds(3) };
        handler.SslOptions.CertificateChainPolicy = new X509ChainPolicy
        {
            TrustMode = X509ChainTrustMode.CustomRootTrust, RevocationMode = X509RevocationMode.NoCheck
        };
        handler.SslOptions.CertificateChainPolicy.CustomTrustStore.Add(trust ?? X509CertificateLoader.LoadCertificateFromFile(
            Path.Combine(TestSettings.Root, ".local", "certificates", "ca.crt")));
        if (captureBob) bobHandler = handler;
        return new HttpClient(handler) { BaseAddress = Origin, Timeout = TimeSpan.FromSeconds(15) };
    }
    private static async Task<string> Csrf(HttpClient client)
    {
        var token = await client.GetFromJsonAsync<JsonElement>("/auth/csrf");
        return token.GetProperty("requestToken").GetString()!;
    }
    private static async Task Login(HttpClient client, string name)
    {
        var seed = TestSettings.Configuration.Users.Single(user => user.UserName == name);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/auth/login")
        {
            Content = JsonContent.Create(new LoginRequest(seed.UserName, seed.Password))
        };
        request.Headers.Add("Origin", "https://localhost:9443");
        request.Headers.Add("X-AZURELAB-XSRF-TOKEN", await Csrf(client));
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
