using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TourLab.Azure.Infrastructure.Persistence;

namespace TourLab.Azure.Gateway.Identity;

public static class IdentitySetup
{
    public static void AddLabIdentity(this WebApplicationBuilder builder)
    {
        var services = builder.Services;
        services.AddRuntimeDatabase("identity");
        services.AddDbContextFactory<IdentityDb>((provider, options) => options.UseSqlServer(
            provider.GetRequiredService<IOptions<RuntimeDatabase>>().Value.ConnectionString,
            sql => { sql.MigrationsHistoryTable("__EFMigrationsHistory", "identity"); sql.CommandTimeout(5); }));

        services.AddIdentity<LabUser, IdentityRole>(options =>
        {
            options.Password.RequiredLength = 12;
            options.User.RequireUniqueEmail = true;
            options.Lockout.MaxFailedAccessAttempts = 5;
            options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
        }).AddEntityFrameworkStores<IdentityDb>().AddUserManager<RequestUserManager>().AddDefaultTokenProviders();

        services.ConfigureApplicationCookie(options =>
        {
            options.Cookie.Name = "__Host-tourlab-azure";
            options.Cookie.Path = "/";
            options.Cookie.HttpOnly = true;
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            options.Cookie.SameSite = SameSiteMode.Strict;
            options.ExpireTimeSpan = TimeSpan.FromMinutes(30);
            options.SlidingExpiration = false;
            options.Events.OnRedirectToLogin = context => { context.Response.StatusCode = 401; return Task.CompletedTask; };
            options.Events.OnRedirectToAccessDenied = context => { context.Response.StatusCode = 403; return Task.CompletedTask; };
        });
        services.Configure<SecurityStampValidatorOptions>(options => options.ValidationInterval = TimeSpan.FromMinutes(1));
        services.AddAuthorization(options => options.AddPolicy("Browser", policy =>
            policy.AddAuthenticationSchemes(IdentityConstants.ApplicationScheme).RequireAuthenticatedUser()));
        services.AddAntiforgery(options =>
        {
            options.HeaderName = "X-AZURELAB-XSRF-TOKEN";
            options.Cookie.Name = "__Host-tourlab-azure-csrf";
            options.Cookie.Path = "/";
            options.Cookie.HttpOnly = true;
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            options.Cookie.SameSite = SameSiteMode.Strict;
        });
        var certificate = X509CertificateLoader.LoadPkcs12FromFile(
            builder.Configuration["DataProtection:CertificatePath"] ?? throw new InvalidOperationException("Data Protection certificate is required."),
            builder.Configuration["DataProtection:CertificatePassword"], X509KeyStorageFlags.EphemeralKeySet);
        services.AddSingleton<X509Certificate2>(_ => certificate);
        services.AddDataProtection().SetApplicationName("TourLab.Azure.Gateway")
            .PersistKeysToDbContext<IdentityDb>().ProtectKeysWithCertificate(certificate);
        services.AddSingleton<HumanTokens>();
    }
}
