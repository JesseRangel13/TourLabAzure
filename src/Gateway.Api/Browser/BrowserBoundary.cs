using Microsoft.AspNetCore.Antiforgery;

namespace TourLab.Azure.Gateway.Browser;

public static class BrowserBoundary
{
    public static void UseBrowserBoundary(this WebApplication app)
    {
        app.Use(async (context, next) =>
        {
            var path = Uri.UnescapeDataString(context.Request.Path.Value ?? "");
            if (path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase) &&
                path.Split('/').Contains("internal", StringComparer.OrdinalIgnoreCase))
            {
                context.Response.StatusCode = 404;
                return;
            }
            var mutation = !HttpMethods.IsGet(context.Request.Method) &&
                !HttpMethods.IsHead(context.Request.Method) && !HttpMethods.IsOptions(context.Request.Method);
            var browserMutation = mutation && (context.Request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase) ||
                context.Request.Path.Equals("/auth/login", StringComparison.OrdinalIgnoreCase) ||
                context.Request.Path.Equals("/auth/logout", StringComparison.OrdinalIgnoreCase));
            if (browserMutation)
            {
                var origins = app.Configuration.GetSection("Browser:AllowedOrigins").Get<string[]>() ?? [];
                var origin = context.Request.Headers.Origin.ToString();
                if (!context.Request.IsHttps || origins.Length == 0 ||
                    context.Request.Headers["Sec-Fetch-Site"] == "cross-site" ||
                    (origin.Length != 0 && !origins.Contains(origin, StringComparer.Ordinal)))
                {
                    await Results.Problem(statusCode: 400, title: "Untrusted browser request origin",
                        extensions: new Dictionary<string, object?> { ["code"] = "InvalidOrigin" }).ExecuteAsync(context);
                    return;
                }
                try
                {
                    await context.RequestServices.GetRequiredService<IAntiforgery>().ValidateRequestAsync(context);
                }
                catch (AntiforgeryValidationException)
                {
                    await Results.Problem(statusCode: 400, title: "Missing or invalid antiforgery token",
                        extensions: new Dictionary<string, object?> { ["code"] = "InvalidAntiforgery" }).ExecuteAsync(context);
                    return;
                }
            }
            await next(context);
        });
    }
}
