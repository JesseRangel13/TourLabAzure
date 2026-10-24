using TourLab.Azure.Infrastructure;
using TourLab.Azure.Infrastructure.Persistence;
using TourLab.Azure.Gateway.Identity;
using TourLab.Azure.Gateway.Browser;

namespace TourLab.Azure.Gateway;

public partial class Program
{
    public static async Task Main(string[] args)
    {
        var builder = ScaffoldHosting.Create(args);
        builder.AddLabIdentity();
        builder.Services.AddCredentialRateLimit();
        builder.Services.AddLabProxy(builder.Configuration, builder.Environment);
        var app = builder.Build();
        _ = app.Services.GetRequiredService<System.Security.Cryptography.X509Certificates.X509Certificate2>();
        app.MapScaffold("Gateway");
        app.UseAuthentication();
        app.UseBrowserBoundary();
        app.UseAuthorization();
        app.UseRateLimiter();
        app.MapLabAuth();
        app.MapDatabaseReadiness("Browser");
        app.MapReverseProxy();
        app.UseDefaultFiles();
        app.UseStaticFiles();
        app.MapFallback(async context =>
        {
            var index = Path.Combine(app.Environment.WebRootPath ?? Path.Combine(app.Environment.ContentRootPath, "wwwroot"), "index.html");
            if (!HttpMethods.IsGet(context.Request.Method) ||
                !ScaffoldHosting.IsSpaNavigation(context.Request.Path) || !File.Exists(index))
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                return;
            }
            context.Response.ContentType = "text/html; charset=utf-8";
            await context.Response.SendFileAsync(index, context.RequestAborted);
        }).AllowAnonymous();
        await app.RunAsync();
    }
}
