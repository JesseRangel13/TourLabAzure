using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using TourLab.Azure.Gateway.Identity;
using TourLab.Azure.Provisioning;

namespace TourLab.Azure.Tests;

[Collection("SQL Identity")]
public sealed class IdentityHttpTests
{
    private static readonly Uri Origin = new("https://localhost:9443");

    [SqlFact]
    public async Task MissingInvalidAndForeignOriginCsrfFailBeforeLoginAndCookieHasSecurityFlags()
    {
        await using var factory = new AzureFactory<TourLab.Azure.Gateway.Program>("identity");
        using var client = Client(factory);
        var user = TestSettings.Configuration.Users.Single(value => value.UserName == "Alice");
        using (var missing = await client.PostAsJsonAsync("/auth/login", new LoginRequest(user.UserName, user.Password)))
            Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
        var token = await Csrf(client);
        using (var invalid = await SendLogin(client, user, "invalid-token"))
            Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        using (var foreign = await SendLogin(client, user, token, "https://foreign.invalid"))
            Assert.Equal(HttpStatusCode.BadRequest, foreign.StatusCode);
        using var login = await SendLogin(client, user, token);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var auth = login.Headers.GetValues("Set-Cookie").Single(value => value.StartsWith("__Host-tourlab-azure=", StringComparison.Ordinal));
        Assert.True(auth.Contains("secure", StringComparison.OrdinalIgnoreCase), "Authentication cookie must be Secure.");
        Assert.True(auth.Contains("httponly", StringComparison.OrdinalIgnoreCase), "Authentication cookie must be HttpOnly.");
        Assert.True(auth.Contains("samesite=strict", StringComparison.OrdinalIgnoreCase), "Authentication cookie must be SameSite Strict.");
        Assert.False(auth.Contains("domain=", StringComparison.OrdinalIgnoreCase), "Host cookie must not specify Domain.");
        using var logoutMissing = await client.PostAsJsonAsync("/auth/logout", new { });
        Assert.Equal(HttpStatusCode.BadRequest, logoutMissing.StatusCode);
        using var oldAnonymousToken = new HttpRequestMessage(HttpMethod.Post, "/auth/logout") { Content = JsonContent.Create(new { }) };
        oldAnonymousToken.Headers.Add("X-AZURELAB-XSRF-TOKEN", token);
        using var stale = await client.SendAsync(oldAnonymousToken);
        Assert.Equal(HttpStatusCode.BadRequest, stale.StatusCode);
    }

    [SqlFact]
    public async Task ConcurrentAliceBobAndAdminClientsKeepTheirOwnIdentityAndLogoutOnlyViaValidatedMutation()
    {
        await using var factory = new AzureFactory<TourLab.Azure.Gateway.Program>("identity");
        using var alice = Client(factory);
        using var bob = Client(factory);
        using var admin = Client(factory);
        var clients = new[] { alice, bob, admin };
        var names = new[] { "Alice", "Bob", "Admin" };
        await Task.WhenAll(clients.Select((client, index) => Login(client, names[index])));
        for (var index = 0; index < clients.Length; index++)
        {
            var user = await clients[index].GetFromJsonAsync<CurrentUser>("/auth/me");
            Assert.Equal(names[index], user!.UserName);
            Assert.Equal(names[index] == "Admin" ? "Admin" : "Tourist", user.Role);
        }
        var aliceBefore = await alice.GetFromJsonAsync<CurrentUser>("/auth/me?userId=forged");
        var bobBefore = await bob.GetFromJsonAsync<CurrentUser>("/auth/me");
        Assert.NotEqual(aliceBefore!.UserId, bobBefore!.UserId);
        using var logout = new HttpRequestMessage(HttpMethod.Post, "/auth/logout") { Content = JsonContent.Create(new { }) };
        logout.Headers.Add("X-AZURELAB-XSRF-TOKEN", await Csrf(alice));
        using (var result = await alice.SendAsync(logout)) Assert.Equal(HttpStatusCode.NoContent, result.StatusCode);
        using (var signedOut = await alice.GetAsync("/auth/me")) Assert.Equal(HttpStatusCode.Unauthorized, signedOut.StatusCode);
        Assert.Equal("Bob", (await bob.GetFromJsonAsync<CurrentUser>("/auth/me"))!.UserName);
    }

    [SqlFact]
    public async Task NewGatewayInstanceReusesEncryptedSqlKeyRingAndRejectsAnExpiredCookie()
    {
        string cookie;
        await using (var first = new AzureFactory<TourLab.Azure.Gateway.Program>("identity"))
        {
            using var client = Client(first);
            var user = TestSettings.Configuration.Users.Single(value => value.UserName == "Alice");
            using var login = await SendLogin(client, user, await Csrf(client));
            Assert.Equal(HttpStatusCode.OK, login.StatusCode);
            cookie = login.Headers.GetValues("Set-Cookie").Single(value => value.StartsWith("__Host-tourlab-azure=", StringComparison.Ordinal)).Split(';')[0];
        }
        await using var restarted = new AzureFactory<TourLab.Azure.Gateway.Program>("identity");
        using var after = Client(restarted, cookies: false);
        after.DefaultRequestHeaders.Add("Cookie", cookie);
        using var resumed = await after.GetAsync("/auth/me");
        Assert.Equal(HttpStatusCode.OK, resumed.StatusCode);
        var contextFactory = restarted.Services.GetRequiredService<IDbContextFactory<IdentityDb>>();
        await using (var database = await contextFactory.CreateDbContextAsync())
        {
            var keys = await database.DataProtectionKeys.AsNoTracking().Select(key => key.Xml).ToArrayAsync();
            Assert.NotEmpty(keys);
            Assert.All(keys, xml =>
            {
                Assert.True(xml!.Contains("encryptedSecret", StringComparison.Ordinal), "Persisted Data Protection key must be encrypted.");
                Assert.False(xml!.Contains("<masterKey", StringComparison.Ordinal), "Plaintext Data Protection master key must not be persisted.");
            });
        }
        var cookies = restarted.Services.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>().Get(IdentityConstants.ApplicationScheme);
        var ticket = cookies.TicketDataFormat.Unprotect(cookie[(cookie.IndexOf('=') + 1)..]);
        Assert.NotNull(ticket);
        ticket!.Properties.IssuedUtc = DateTimeOffset.UtcNow.AddMinutes(-31);
        ticket.Properties.ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(-1);
        after.DefaultRequestHeaders.Remove("Cookie");
        after.DefaultRequestHeaders.Add("Cookie", "__Host-tourlab-azure=" + cookies.TicketDataFormat.Protect(ticket));
        using var expired = await after.GetAsync("/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, expired.StatusCode);
    }

    [SqlFact]
    public async Task ToolTokenIsCredentialBasedNotCookieOrUserSelectionAndDoesNotAcceptBrowserOrigin()
    {
        await using var factory = new AzureFactory<TourLab.Azure.Gateway.Program>("identity");
        using var client = Client(factory);
        var alice = TestSettings.Configuration.Users.Single(value => value.UserName == "Alice");
        using (var invalid = await client.PostAsJsonAsync("/auth/token", new TokenRequest("absent-user", "invalid", "catalog")))
            Assert.Equal(HttpStatusCode.Unauthorized, invalid.StatusCode);
        using var browserRequest = new HttpRequestMessage(HttpMethod.Post, "/auth/token")
        {
            Content = JsonContent.Create(new TokenRequest(alice.UserName, alice.Password, "catalog"))
        };
        browserRequest.Headers.Add("Origin", Origin.AbsoluteUri.TrimEnd('/'));
        using (var browser = await client.SendAsync(browserRequest)) Assert.Equal(HttpStatusCode.Forbidden, browser.StatusCode);
        using var valid = await client.PostAsJsonAsync("/auth/token", new TokenRequest(alice.UserName, alice.Password, "catalog"));
        Assert.Equal(HttpStatusCode.OK, valid.StatusCode);
        var payload = await valid.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(string.IsNullOrWhiteSpace(payload.GetProperty("accessToken").GetString()));
        Assert.Equal("no-store", valid.Headers.CacheControl!.ToString());
    }

    [SqlFact]
    public async Task CredentialRateLimitIgnoresForgedForwardedIp()
    {
        await using var factory = new AzureFactory<TourLab.Azure.Gateway.Program>("identity");
        using var client = Client(factory);
        for (var index = 0; index < 10; index++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "/auth/token")
            {
                Content = JsonContent.Create(new TokenRequest("absent-" + index, "invalid", "catalog"))
            };
            request.Headers.Add("X-Forwarded-For", $"203.0.113.{index + 1}");
            using var result = await client.SendAsync(request);
            Assert.Equal(HttpStatusCode.Unauthorized, result.StatusCode);
        }
        using var limited = await client.PostAsJsonAsync("/auth/token", new TokenRequest("absent-next", "invalid", "catalog"));
        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
    }

    private static HttpClient Client(AzureFactory<TourLab.Azure.Gateway.Program> factory, bool cookies = true) =>
        factory.CreateClient(new() { BaseAddress = Origin, AllowAutoRedirect = false, HandleCookies = cookies });

    private static async Task<string> Csrf(HttpClient client)
    {
        using var response = await client.GetAsync("/auth/csrf");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var readable = response.Headers.GetValues("Set-Cookie").Single(value => value.StartsWith("AZURELAB-XSRF-TOKEN=", StringComparison.Ordinal));
        Assert.False(readable.Contains("httponly", StringComparison.OrdinalIgnoreCase), "XSRF request token must be readable by Angular.");
        return body.GetProperty("requestToken").GetString()!;
    }

    private static Task<HttpResponseMessage> SendLogin(HttpClient client, SeedUser user, string token, string? origin = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/auth/login") { Content = JsonContent.Create(new LoginRequest(user.UserName, user.Password)) };
        request.Headers.Add("X-AZURELAB-XSRF-TOKEN", token);
        request.Headers.Add("Origin", origin ?? Origin.AbsoluteUri.TrimEnd('/'));
        return SendAndDispose(client, request);
    }
    private static async Task<HttpResponseMessage> SendAndDispose(HttpClient client, HttpRequestMessage request)
    {
        using (request) return await client.SendAsync(request);
    }
    private static async Task Login(HttpClient client, string name)
    {
        var user = TestSettings.Configuration.Users.Single(value => value.UserName == name);
        using var response = await SendLogin(client, user, await Csrf(client));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
