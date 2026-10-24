using System.Diagnostics;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
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
        var privatePath = Environment.GetEnvironmentVariable("AZURELAB_CONFIG");
        if (!string.IsNullOrWhiteSpace(privatePath))
            builder.Configuration.AddJsonFile(Path.GetFullPath(privatePath), optional: false, reloadOnChange: false);
        builder.Configuration.AddEnvironmentVariables("AZURELAB_");
        builder.Logging.ClearProviders();
        builder.Logging.AddJsonConsole();
        builder.Logging.AddFilter("Microsoft.EntityFrameworkCore.Database.Command", LogLevel.Warning);
        builder.Logging.AddFilter("Microsoft.EntityFrameworkCore.Update", LogLevel.None);
        builder.Logging.AddFilter("Microsoft.AspNetCore.Hosting.Diagnostics", LogLevel.Warning);
        builder.Logging.AddFilter("Microsoft.AspNetCore.Diagnostics", LogLevel.None);
        builder.Logging.AddFilter("System.Net.Http.HttpClient", LogLevel.Warning);
        builder.Services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
        {
            context.ProblemDetails.Extensions.TryAdd("code", "HttpError");
            context.ProblemDetails.Extensions["traceId"] = Activity.Current?.Id ?? context.HttpContext.TraceIdentifier;
        });
        builder.Services.AddExceptionHandler<SafeExceptionHandler>();
        builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = 5 * 1024 * 1024);
        return builder;
    }

    public static void MapScaffold(this WebApplication app, string service)
    {
        app.UseExceptionHandler();
        app.UseStatusCodePages(async status =>
        {
            await Results.Problem(statusCode: status.HttpContext.Response.StatusCode,
                title: "The request could not be completed").ExecuteAsync(status.HttpContext);
        });
        app.Use(async (context, next) =>
        {
            context.Response.Headers["X-Content-Type-Options"] = "nosniff";
            context.Response.Headers["Referrer-Policy"] = "same-origin";
            context.Response.Headers["X-Frame-Options"] = "DENY";
            await next(context);
        });
        app.MapGet("/health/live", () => new Liveness(service, "Alive", "AZ-002")).AllowAnonymous();
    }

    public static bool IsSpaNavigation(PathString path) =>
        !path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase) &&
        !path.StartsWithSegments("/auth", StringComparison.OrdinalIgnoreCase) &&
        !path.StartsWithSegments("/health", StringComparison.OrdinalIgnoreCase) &&
        !Path.HasExtension(path.Value);
}

public sealed class SafeExceptionHandler(ILogger<SafeExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        if (exception is OperationCanceledException && context.RequestAborted.IsCancellationRequested) return false;
        if (exception is BadHttpRequestException invalid)
        {
            await Results.Problem(statusCode: invalid.StatusCode, title: "Invalid request",
                extensions: new Dictionary<string, object?> { ["code"] = "InvalidRequest" }).ExecuteAsync(context);
            return true;
        }
        var dependency = exception is SqlException or DbUpdateException;
        logger.LogWarning("Request failed with classified type {FailureType}", exception.GetType().Name);
        await Results.Problem(statusCode: dependency ? 503 : 500,
            title: dependency ? "A required dependency is unavailable" : "The request could not be completed",
            extensions: new Dictionary<string, object?> { ["code"] = dependency ? "DatabaseUnavailable" : "UnexpectedError" })
            .ExecuteAsync(context);
        return true;
    }
}
