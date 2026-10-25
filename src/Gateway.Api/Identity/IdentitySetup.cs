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
        var protection=services.AddDataProtection().SetApplicationName("TourLab.Azure.Gateway")
            .PersistKeysToDbContext<IdentityDb>();
        if(TourLab.Azure.Infrastructure.Security.AzureSecurity.Cloud(builder.Configuration)) {
            protection.ProtectKeysWithAzureKeyVault(new Uri(builder.Configuration["DataProtection:KeyIdentifier"]!),
                TourLab.Azure.Infrastructure.Security.AzureSecurity.KeyResolver());
        } else {
            var certificates=TourLab.Azure.Infrastructure.Security.ProtectionCertificates.Load(
                builder.Configuration.GetSection("DataProtection"),builder.Environment.IsDevelopment());
            services.AddSingleton(_=>certificates);
            protection.ProtectKeysWithCertificate(certificates.Active).UnprotectKeysWithAnyCertificate(certificates.All);
        }
        services.AddSingleton<HumanTokens>();
    }
}
