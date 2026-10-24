using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TourLab.Azure.Infrastructure;
using TourLab.Azure.Infrastructure.Persistence;
using TourLab.Azure.Infrastructure.Security;
using TourLab.Azure.Notifications.Persistence;

namespace TourLab.Azure.Notifications;

public partial class Program
{
    public static async Task Main(string[] args)
    {
        var builder = ScaffoldHosting.Create(args);
        builder.Services.AddRuntimeDatabase("notifications");
        builder.Services.AddDbContext<NotificationsDb>((services, options) => options.UseSqlServer(
            services.GetRequiredService<IOptions<RuntimeDatabase>>().Value.ConnectionString,
            sql => { sql.MigrationsHistoryTable("__EFMigrationsHistory", "notifications"); sql.CommandTimeout(5); }));
        builder.Services.AddBackendSecurity("Notifications");

        var app = builder.Build();
        app.MapScaffold("Notifications");
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapDatabaseReadiness(BackendSecurity.Human);
        app.MapBackendBoundary("Notifications");

        await app.RunAsync();
    }
}
