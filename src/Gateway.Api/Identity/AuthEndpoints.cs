using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;

namespace TourLab.Azure.Gateway.Identity;

public sealed record LoginRequest(string? UserName, string? Password);
public sealed record TokenRequest(string? UserName, string? Password, string? Service);
public sealed record CurrentUser(string UserId, string UserName, string Role);

public static class AuthEndpoints
{
    public static void AddCredentialRateLimit(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = 429;
            options.AddPolicy("Credentials", context => RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 10, Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0, AutoReplenishment = true
                }));
            options.OnRejected = (context, ct) => new ValueTask(Results.Problem(
                statusCode: 429, title: "Credential request limit reached",
                extensions: new Dictionary<string, object?> { ["code"] = "CredentialRateLimit" }).ExecuteAsync(context.HttpContext));
        });
    }

    public static void MapLabAuth(this WebApplication app)
    {
        app.MapGet("/auth/csrf", (HttpContext context, IAntiforgery antiforgery) =>
        {
            var token = antiforgery.GetAndStoreTokens(context).RequestToken!;
            context.Response.Headers.CacheControl = "no-store";
            context.Response.Cookies.Append("AZURELAB-XSRF-TOKEN", token, new CookieOptions
            {
                HttpOnly = false, Secure = true, SameSite = SameSiteMode.Strict, Path = "/"
            });
            return Results.Ok(new { requestToken = token });
        }).AllowAnonymous();

        app.MapPost("/auth/login", async (LoginRequest request, HttpContext context,
            UserManager<LabUser> users, SignInManager<LabUser> signIn) =>
        {
            if (!ValidCredentials(request.UserName, request.Password)) return InvalidLogin();
            var user = await users.FindByNameAsync(request.UserName!);
            if (user is null) return InvalidLogin();
            var check = await signIn.CheckPasswordSignInAsync(user, request.Password!, lockoutOnFailure: true);
            if (!check.Succeeded) return InvalidLogin();
            var roles = await users.GetRolesAsync(user);
            if (roles.Count != 1 || roles[0] is not ("Tourist" or "Admin")) return InvalidLogin();
            await signIn.SignInAsync(user, isPersistent: false);
            context.Response.Headers.CacheControl = "no-store";
            context.Response.Cookies.Delete("AZURELAB-XSRF-TOKEN", CookieDeletion());
            return Results.Ok(new CurrentUser(user.Id, user.UserName!, roles[0]));
        }).RequireRateLimiting("Credentials").AllowAnonymous();

        app.MapPost("/auth/logout", async (HttpContext context, UserManager<LabUser> users, SignInManager<LabUser> signIn) =>
        {
            var user = await users.GetUserAsync(context.User);
            if (user is not null)
            {
                var changed = await users.UpdateSecurityStampAsync(user);
                if (!changed.Succeeded) return Results.Problem(statusCode: 503, title: "Session invalidation could not be persisted",
                    extensions: new Dictionary<string, object?> { ["code"] = "LogoutUnavailable" });
            }
            await signIn.SignOutAsync();
            context.Response.Cookies.Delete("AZURELAB-XSRF-TOKEN", CookieDeletion());
            context.Response.Headers.CacheControl = "no-store";
            return Results.NoContent();
        }).RequireAuthorization("Browser");

        app.MapGet("/auth/me", (HttpContext context) =>
        {
            context.Response.Headers.CacheControl = "no-store";
            return Results.Ok(new CurrentUser(
                context.User.FindFirstValue(ClaimTypes.NameIdentifier)!,
                context.User.Identity!.Name!,
                context.User.FindFirstValue(ClaimTypes.Role)!));
        }).RequireAuthorization("Browser");

        // API tools only; browser auth is cookie-based. This endpoint never accepts a role or owner ID.
        app.MapPost("/auth/token", async (TokenRequest request, HttpContext context,
            UserManager<LabUser> users, SignInManager<LabUser> signIn, HumanTokens tokens) =>
        {
            if (context.Request.Headers.ContainsKey("Origin") || context.Request.Headers.ContainsKey("Sec-Fetch-Site"))
                return Results.Problem(statusCode: 403, title: "API-tool credential endpoint",
                    extensions: new Dictionary<string, object?> { ["code"] = "ToolEndpointOnly" });
            if (!ValidCredentials(request.UserName, request.Password) ||
                !HumanTokens.Services.Contains(request.Service ?? "", StringComparer.Ordinal)) return InvalidLogin();
            var user = await users.FindByNameAsync(request.UserName!);
            if (user is null) return InvalidLogin();
            var result = await signIn.CheckPasswordSignInAsync(user, request.Password!, lockoutOnFailure: true);
            if (!result.Succeeded) return InvalidLogin();
            var roles = await users.GetRolesAsync(user);
            if (roles.Count != 1 || roles[0] is not ("Tourist" or "Admin")) return InvalidLogin();
            context.Response.Headers.CacheControl = "no-store";
            return Results.Ok(new { accessToken = tokens.Issue(user.Id, user.UserName!, roles[0], request.Service!, TimeSpan.FromMinutes(15)), expiresIn = 900 });
        }).RequireRateLimiting("Credentials").AllowAnonymous();
    }

    private static bool ValidCredentials(string? name, string? password) =>
        !string.IsNullOrWhiteSpace(name) && name.Length <= 100 && !string.IsNullOrEmpty(password) && password.Length <= 256;
    private static IResult InvalidLogin() => Results.Problem(statusCode: 401, title: "Invalid credentials",
        extensions: new Dictionary<string, object?> { ["code"] = "InvalidCredentials" });
    private static CookieOptions CookieDeletion() => new() { Secure = true, HttpOnly = false, SameSite = SameSiteMode.Strict, Path = "/" };
}
