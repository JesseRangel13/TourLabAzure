using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using TourLab.Azure.Catalog.Application;
using TourLab.Azure.Infrastructure.Security;
using TourLab.Azure.Reservations;
namespace TourLab.Azure.Tests;

[Collection("SQL Identity")]
public sealed class CatalogHttpTests
{
    private static HttpClient Client(AzureFactory<TourLab.Azure.Catalog.Program> factory, string role)
    {
        var client = factory.CreateClient();
        using var rsa = RSA.Create();
        rsa.ImportFromPem(File.ReadAllText(Path.Combine(TestSettings.Output, "human-signing.key")));
        var now = DateTime.UtcNow.AddSeconds(-1);
        var token = new JwtSecurityToken(BackendSecurity.Issuer, BackendSecurity.Audience("Catalog"),
            [new Claim("sub", "catalog-http-" + role), new Claim("role", role)],
            now, now.AddMinutes(2), new SigningCredentials(new RsaSecurityKey(rsa)
            { CryptoProviderFactory = new CryptoProviderFactory { CacheSignatureProviders = false } }, SecurityAlgorithms.RsaSha256));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", new JwtSecurityTokenHandler().WriteToken(token));
        return client;
    }

    [SqlFact]
    public async Task CrudEnforcesAdminVersionsVisibilityRequiredFieldsAndPagination()
    {
        await using var factory = new AzureFactory<TourLab.Azure.Catalog.Program>("catalog");
        using var admin = Client(factory, "Admin");
        using var tourist = Client(factory, "Tourist");
        using (var anonymous = factory.CreateClient())
        using (var deniedSchema = await anonymous.GetAsync("/v1/openapi/v1.json"))
            Assert.Equal(HttpStatusCode.Unauthorized, deniedSchema.StatusCode);
        var schema = await admin.GetFromJsonAsync<JsonElement>("/v1/openapi/v1.json");
        Assert.True(schema.GetProperty("paths").TryGetProperty("/v1/tours", out _));
        Assert.True(schema.GetProperty("components").GetProperty("schemas").TryGetProperty("TourView", out _));
        var name = "HTTP " + Guid.NewGuid();
        using (var denied = await tourist.PostAsJsonAsync("/v1/admin/tours", new CreateTour(name, "")))
            Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        using var created = await admin.PostAsJsonAsync("/v1/admin/tours", new CreateTour(name, "HTTP evidence"));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var tour = (await created.Content.ReadFromJsonAsync<TourView>())!;
        using var createdSession = await admin.PostAsJsonAsync($"/v1/admin/tours/{tour.Id}/sessions", new CreateSession(DateTimeOffset.UtcNow.AddDays(2), 12345, 4));
        Assert.Equal(HttpStatusCode.Created, createdSession.StatusCode);
        var session = (await createdSession.Content.ReadFromJsonAsync<SessionView>())!;
        using var price = await admin.PutAsJsonAsync($"/v1/admin/sessions/{session.Id}/price", new UpdatePrice(22222, session.Version));
        Assert.Equal(HttpStatusCode.OK, price.StatusCode);
        var priced = (await price.Content.ReadFromJsonAsync<SessionView>())!;
        using var stale = await admin.PutAsJsonAsync($"/v1/admin/sessions/{session.Id}/capacity", new UpdateCapacity(5, session.Version));
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        var problem = await stale.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("VersionConflict", problem.GetProperty("code").GetString());
        Assert.False(string.IsNullOrWhiteSpace(problem.GetProperty("traceId").GetString()));
        using var capacity = await admin.PutAsJsonAsync($"/v1/admin/sessions/{session.Id}/capacity", new UpdateCapacity(5, priced.Version));
        Assert.Equal(HttpStatusCode.OK, capacity.StatusCode);
        var page = await tourist.GetFromJsonAsync<TourPage>($"/v1/tours?search={Uri.EscapeDataString(name)}&pageSize=1");
        Assert.Equal(1, page!.Total); Assert.Equal(tour.Id, Assert.Single(page.Items).Id);
        using (var invalid = await admin.GetAsync("/v1/tours?page=0")) Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        using (var invalid = await admin.GetAsync("/v1/tours?pageSize=51")) Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        using (var missingField = await admin.PutAsJsonAsync($"/v1/admin/tours/{tour.Id}", new { name, description = "", expectedVersion = tour.Version }))
            Assert.Equal(HttpStatusCode.BadRequest, missingField.StatusCode);
        using var inactive = await admin.PutAsJsonAsync($"/v1/admin/tours/{tour.Id}", new UpdateTour(name, "", false, tour.Version));
        Assert.Equal(HttpStatusCode.OK, inactive.StatusCode);
        using (var hidden = await tourist.GetAsync($"/v1/tours/{tour.Id}")) Assert.Equal(HttpStatusCode.NotFound, hidden.StatusCode);
        using (var hidden = await tourist.GetAsync($"/v1/tours/{tour.Id}/sessions")) Assert.Equal(HttpStatusCode.NotFound, hidden.StatusCode);
        Assert.Empty((await tourist.GetFromJsonAsync<TourPage>($"/v1/tours?search={Uri.EscapeDataString(name)}"))!.Items);
        Assert.False((await admin.GetFromJsonAsync<TourView>($"/v1/tours/{tour.Id}"))!.Active);
    }

    [SqlFact]
    public async Task ServiceOnlyQuotePersistsIdempotentPriceSnapshotAndRejectsChangedContent()
    {
        await using var factory = new AzureFactory<TourLab.Azure.Catalog.Program>("catalog");
        using var admin = Client(factory, "Admin");
        using var created = await admin.PostAsJsonAsync("/v1/admin/tours", new CreateTour("Quote HTTP " + Guid.NewGuid(), ""));
        var tour = (await created.Content.ReadFromJsonAsync<TourView>())!;
        using var createdSession = await admin.PostAsJsonAsync($"/v1/admin/tours/{tour.Id}/sessions", new CreateSession(DateTimeOffset.UtcNow.AddDays(2), 500, 6));
        var session = (await createdSession.Content.ReadFromJsonAsync<SessionView>())!;
        var request = new CreateQuote(Guid.NewGuid(), session.Id, 2, "Alice", 500, "MXN");
        using (var forbidden = await admin.PostAsJsonAsync("/v1/internal/quotes", request))
            Assert.Equal(HttpStatusCode.Unauthorized, forbidden.StatusCode);
        using var service = factory.CreateClient();
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        { ["Quote:PrivateKeyPath"] = Path.Combine(TestSettings.Output, "quote-signing.key") }).Build();
        using var tokens = new QuoteServiceTokens(config, TimeProvider.System);
        service.DefaultRequestHeaders.Authorization = new("Bearer", tokens.Issue());
        using var quoteResponse = await service.PostAsJsonAsync("/v1/internal/quotes", request);
        Assert.Equal(HttpStatusCode.OK, quoteResponse.StatusCode);
        var quote = (await quoteResponse.Content.ReadFromJsonAsync<QuoteView>())!;
        Assert.Equal(1000, quote.TotalAmountMinor); Assert.Equal("MXN", quote.Currency);
        using var replayResponse = await service.PostAsJsonAsync("/v1/internal/quotes", request);
        Assert.Equal(quote.QuoteId, (await replayResponse.Content.ReadFromJsonAsync<QuoteView>())!.QuoteId);
        using var changed = await service.PostAsJsonAsync("/v1/internal/quotes", request with { Participants = 3 });
        Assert.Equal(HttpStatusCode.Conflict, changed.StatusCode);
        Assert.Equal("RequestIdConflict", (await changed.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
    }
}