using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TourLab.Azure.Contracts;

namespace TourLab.Azure.Infrastructure;

public static class ScaffoldHosting
{
    public static WebApplicationBuilder Create(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.Configuration.AddEnvironmentVariables("AZURELAB_");
        builder.Logging.ClearProviders();
        builder.Logging.AddJsonConsole();
        builder.Services.AddProblemDetails();
        builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = 5 * 1024 * 1024);
        return builder;
    }

    public static void MapScaffold(this WebApplication app, string service)
    {
        app.UseExceptionHandler();
        app.Use(async (context, next) =>
        {
            context.Response.Headers["X-Content-Type-Options"] = "nosniff";
            await next(context);
        });
        app.MapGet("/health/live", () => new Liveness(service, "Alive", "AZ-001"));
        // Liveness is real process evidence; domain dependencies are deliberately not ready yet.
        app.MapGet("/health/ready", () => Results.Problem(
            statusCode: StatusCodes.Status503ServiceUnavailable,
            title: "Domain dependencies are not configured in AZ-001",
            extensions: new Dictionary<string, object?> { ["code"] = "ScaffoldNotReady" }));
    }

    public static bool IsSpaNavigation(PathString path) =>
        !path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase) &&
        !path.StartsWithSegments("/auth", StringComparison.OrdinalIgnoreCase) &&
        !path.StartsWithSegments("/health", StringComparison.OrdinalIgnoreCase) &&
        !Path.HasExtension(path.Value);
}
