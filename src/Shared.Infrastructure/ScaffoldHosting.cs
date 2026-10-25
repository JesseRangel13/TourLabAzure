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
    public static async Task<WebApplicationBuilder> CreateAsync(string[] args, string workload, CancellationToken ct = default)
    {
        var builder = WebApplication.CreateBuilder(args);
        var privatePath = Environment.GetEnvironmentVariable("AZURELAB_CONFIG");
        if (!string.IsNullOrWhiteSpace(privatePath))
            builder.Configuration.AddJsonFile(Path.GetFullPath(privatePath), optional: false, reloadOnChange: false);
        builder.Configuration.AddEnvironmentVariables("AZURELAB_");
        await TourLab.Azure.Infrastructure.Security.StartupSecrets.LoadAsync(builder.Configuration, builder.Environment.IsDevelopment(), workload, ct);
        builder.Configuration.AddInMemoryCollection(new Dictionary<string,string?> { ["AzureSecurity:Workload"] = workload });
        builder.Logging.ClearProviders();
        builder.Logging.AddJsonConsole(o=>o.IncludeScopes=true);
        builder.Logging.Configure(o=>o.ActivityTrackingOptions=ActivityTrackingOptions.TraceId|ActivityTrackingOptions.SpanId|ActivityTrackingOptions.ParentId);
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
        if(TourLab.Azure.Infrastructure.Security.AzureSecurity.Cloud(app.Configuration)) {
            app.UseForwardedHeaders(TourLab.Azure.Infrastructure.Security.CloudIngress.Options(app.Configuration));
            app.Use(async(context,next)=>{
                if(!context.Request.IsHttps){context.Response.StatusCode=400;return;}
                await next(context);
            });
        }
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
            Exception? failure=null;
            try { await next(context); }
            catch(Exception error){failure=error;throw;}
            finally {
                var route=(context.GetEndpoint() as Microsoft.AspNetCore.Routing.RouteEndpoint)?.RoutePattern.RawText??"unmatched";
                var log=context.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("TourLab.Azure.Http");
                log.LogInformation("HTTP {Method} {Route} {Outcome} {StatusCode}",context.Request.Method,route,failure is null?"Completed":"DeferredToErrorHandler",failure is null?context.Response.StatusCode:(int?)null);
            }
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
