using TourLab.Azure.Notifications.Application;
using TourLab.Azure.Infrastructure.Messaging;
using TourLab.Azure.Infrastructure.Operations;
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
        builder.Services.AddDbContextFactory<NotificationsDb>((services, options) => options.UseSqlServer(
            services.GetRequiredService<IOptions<RuntimeDatabase>>().Value.ConnectionString,
            sql => { sql.MigrationsHistoryTable("__EFMigrationsHistory", "notifications"); sql.CommandTimeout(5); }), ServiceLifetime.Scoped);
        builder.Services.AddBackendSecurity("Notifications");

        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddSingleton<WorkerStatus>();
        builder.Services.Configure<MessagingOptions>(builder.Configuration.GetSection("Messaging"));
        builder.Services.AddLabMessaging("notifications");
        builder.Services.AddScoped<IInboxConsumer,NotificationConsumer>();
        builder.Services.AddScoped<INotificationSender,DurableFakeSender>();
        builder.Services.AddScoped<NotificationProcessor>();
        builder.Services.Configure<NotificationWorkOptions>(o=>o.Enabled=builder.Configuration.GetValue<bool>("Messaging:Enabled"));
        builder.Services.AddHostedService<NotificationWorker>();
        builder.Services.ConfigureHttpJsonOptions(o=>o.SerializerOptions.RespectRequiredConstructorParameters=true);
        var app = builder.Build();
        app.MapScaffold("Notifications");
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapDatabaseReadiness(BackendSecurity.Human);
        app.MapBackendBoundary("Notifications");

        app.MapNotifications();
        await app.RunAsync();
    }
}
