using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace TourLab.Azure.Infrastructure.Security;

public static class BackendSecurity
{
    public const string Human = "Human";
    public const string Admin = "Admin";
    public const string Quote = "QuoteService";
    public const string Issuer = "tourlab-azure-identity";

    public static string Audience(string service) => $"tourlab-azure-{service.ToLowerInvariant()}-api";

    public static TokenValidationParameters Parameters(string keyPath, string issuer, string audience)
    {
        using var rsa = RSA.Create();
        rsa.ImportFromPem(File.ReadAllText(keyPath));
        return new()
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new RsaSecurityKey(rsa.ExportParameters(false)),
            RequireSignedTokens = true,
            ValidAlgorithms = [SecurityAlgorithms.RsaSha256],
            ValidateIssuer = true, ValidIssuer = issuer,
            ValidateAudience = true, ValidAudience = audience,
            ValidateLifetime = true, RequireExpirationTime = true,
            ClockSkew = TimeSpan.FromSeconds(5),
            NameClaimType = "name", RoleClaimType = "role"
        };
    }

    public static void AddBackendSecurity(this IServiceCollection services, string service)
    {
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IConfiguration>((options, config) =>
            {
                options.MapInboundClaims = false;
                options.IncludeErrorDetails = false;
                options.TokenValidationParameters = Parameters(
                    config["Jwt:PublicKeyPath"] ?? throw new InvalidOperationException("Backend validation key is required."),
                    Issuer, Audience(service));
                options.Events = new JwtBearerEvents
                {
                    OnTokenValidated = context =>
                    {
                        var principal = context.Principal!;
                        var subject = principal.FindAll("sub").ToArray();
                        var roles = principal.FindAll("role").ToArray();
                        if (subject.Length != 1 || string.IsNullOrWhiteSpace(subject[0].Value) || subject[0].Value.Length > 128 ||
                            roles.Length != 1 || roles[0].Value is not ("Tourist" or "Admin") ||
                            context.SecurityToken.ValidFrom == DateTime.MinValue ||
                            context.SecurityToken.ValidTo - context.SecurityToken.ValidFrom > TimeSpan.FromMinutes(15))
                            context.Fail("Invalid identity claims or bounded token lifetime.");
                        return Task.CompletedTask;
                    }
                };
            });

        if (service == "Catalog")
        {
            services.AddAuthentication().AddJwtBearer(Quote, _ => { });
            services.AddOptions<JwtBearerOptions>(Quote).Configure<IConfiguration>((options, config) =>
            {
                options.MapInboundClaims = false;
                options.IncludeErrorDetails = false;
                options.TokenValidationParameters = Parameters(
                    config["Quote:PublicKeyPath"] ?? throw new InvalidOperationException("Quote validation key is required."),
                    "tourlab-azure-reservations", "tourlab-azure-catalog-quotes");
                options.Events = new JwtBearerEvents
                {
                    OnTokenValidated = context =>
                    {
                        var p = context.Principal!;
                        if (p.FindAll("sub").Count() != 1 || p.FindFirstValue("sub") != "reservations" ||
                            p.FindAll("permission").Count() != 1 || p.FindFirstValue("permission") != "quote:create" ||
                            p.HasClaim(c => c.Type == "role") || context.SecurityToken.ValidFrom == DateTime.MinValue ||
                            context.SecurityToken.ValidTo - context.SecurityToken.ValidFrom > TimeSpan.FromMinutes(2))
                            context.Fail("Invalid quote service identity.");
                        return Task.CompletedTask;
                    }
                };
            });
        }

        services.AddAuthorization(options =>
        {
            var human = new AuthorizationPolicyBuilder(JwtBearerDefaults.AuthenticationScheme).RequireAuthenticatedUser().Build();
            options.AddPolicy(Human, human);
            options.FallbackPolicy = human;
            options.AddPolicy(Admin, new AuthorizationPolicyBuilder(JwtBearerDefaults.AuthenticationScheme).RequireAuthenticatedUser().RequireRole("Admin").Build());
            if (service == "Catalog")
                options.AddPolicy(Quote, new AuthorizationPolicyBuilder(Quote).RequireAuthenticatedUser().RequireClaim("permission", "quote:create").Build());
        });
    }

    public static void MapBackendBoundary(this WebApplication app, string service)
    {
        app.MapGet("/v1/status", (ClaimsPrincipal user) => new
        {
            service, stage = "AZ-002", userId = user.FindFirstValue("sub"), role = user.FindFirstValue("role")
        }).RequireAuthorization(Human);
        app.MapGet("/v1/admin/status", () => new { service, stage = "AZ-002" }).RequireAuthorization(Admin);
    }
}
