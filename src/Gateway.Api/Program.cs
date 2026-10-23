using TourLab.Azure.Infrastructure;

namespace TourLab.Azure.Gateway;

public partial class Program
{
    public static async Task Main(string[] args)
    {
        var builder = ScaffoldHosting.Create(args);
        var app = builder.Build();
        app.MapScaffold("Gateway");
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
        });
        await app.RunAsync();
    }
}
