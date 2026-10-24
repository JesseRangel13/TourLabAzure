using System.Net;
using System.Net.Http.Json;
using TourLab.Azure.Contracts;
using TourLab.Azure.Infrastructure;

namespace TourLab.Azure.Tests;

public sealed class HostTests
{
    private static async Task AssertHost<T>(string name, string schema) where T : class
    {
        await using var factory = new AzureFactory<T>(schema);
        using var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost:9443"), AllowAutoRedirect = false });
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var live = await client.GetFromJsonAsync<Liveness>("/health/live", timeout.Token);
        Assert.Equal(new Liveness(name, "Alive", "AZ-002"), live);
        using var ready = await client.GetAsync("/health/ready", timeout.Token);
        Assert.Equal(HttpStatusCode.Unauthorized, ready.StatusCode);
    }
    [Fact] public Task Gateway() => AssertHost<TourLab.Azure.Gateway.Program>("Gateway", "identity");
    [Fact] public Task Catalog() => AssertHost<TourLab.Azure.Catalog.Program>("Catalog", "catalog");
    [Fact] public Task Reservations() => AssertHost<TourLab.Azure.Reservations.Program>("Reservations", "reservations");
    [Fact] public Task Payments() => AssertHost<TourLab.Azure.Payments.Program>("Payments", "payments");
    [Fact] public Task Notifications() => AssertHost<TourLab.Azure.Notifications.Program>("Notifications", "notifications");

    [Theory]
    [InlineData("/api/missing")]
    [InlineData("/API/missing")]
    [InlineData("/auth/login")]
    [InlineData("/health/unknown")]
    [InlineData("/missing.js")]
    public void ReservedPathsCannotBecomeSpaSuccess(string path) => Assert.False(ScaffoldHosting.IsSpaNavigation(path));
    [Fact] public void ClientDeepLinkCanReachSpa() => Assert.True(ScaffoldHosting.IsSpaNavigation("/account"));
}
