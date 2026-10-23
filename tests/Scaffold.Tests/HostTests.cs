using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using TourLab.Azure.Contracts;
using TourLab.Azure.Infrastructure;

namespace TourLab.Azure.Tests;

public sealed class HostTests
{
    private static async Task AssertHost<T>(string name) where T : class
    {
        await using var factory = new WebApplicationFactory<T>();
        using var client = factory.CreateClient();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var live = await client.GetFromJsonAsync<Liveness>("/health/live", timeout.Token);
        Assert.Equal(new Liveness(name, "Alive", "AZ-001"), live);
        using var ready = await client.GetAsync("/health/ready", timeout.Token);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, ready.StatusCode);
        using var business = await client.GetAsync("/api/catalog/v1/tours", timeout.Token);
        Assert.Equal(HttpStatusCode.NotFound, business.StatusCode);
    }

    [Fact] public Task Gateway() => AssertHost<TourLab.Azure.Gateway.Program>("Gateway");
    [Fact] public Task Catalog() => AssertHost<TourLab.Azure.Catalog.Program>("Catalog");
    [Fact] public Task Reservations() => AssertHost<TourLab.Azure.Reservations.Program>("Reservations");
    [Fact] public Task Payments() => AssertHost<TourLab.Azure.Payments.Program>("Payments");
    [Fact] public Task Notifications() => AssertHost<TourLab.Azure.Notifications.Program>("Notifications");

    [Theory]
    [InlineData("/api/missing")]
    [InlineData("/API/missing")]
    [InlineData("/auth/login")]
    [InlineData("/health/unknown")]
    [InlineData("/missing.js")]
    public void ReservedPathsCannotBecomeSpaSuccess(string path) =>
        Assert.False(ScaffoldHosting.IsSpaNavigation(path));

    [Fact]
    public void ClientDeepLinkCanReachSpa() =>
        Assert.True(ScaffoldHosting.IsSpaNavigation("/reservations/example"));
}
