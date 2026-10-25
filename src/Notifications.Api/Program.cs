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
        var builder = await ScaffoldHosting.CreateAsync(args, "notifications");
        builder.Services.AddRuntimeDatabase("notifications");
        builder.Services.AddDbContextFactory<NotificationsDb>((services, options) => options.UseSqlServer(
            services.GetRequiredService<IOptions<RuntimeDatabase>>().Value.ConnectionString,
            sql => { sql.MigrationsHistoryTable("__EFMigrationsHistory", "notifications"); sql.CommandTimeout(5); }), ServiceLifetime.Scoped);
        builder.Services.AddBackendSecurity("Notifications");

        builder.Services.AddOptions<TourLab.Azure.Notifications.Receipts.ReceiptStorageOptions>()
            .Bind(builder.Configuration.GetSection("ReceiptStorage"))
            .Validate(o => TourLab.Azure.Notifications.Receipts.BlobReceiptStorage.Safe(o, builder.Environment.IsDevelopment()), "Unsafe receipt storage").ValidateOnStart();
        builder.Services.AddSingleton<TourLab.Azure.Notifications.Receipts.IReceiptStorage, TourLab.Azure.Notifications.Receipts.BlobReceiptStorage>();
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddServiceOperations(builder.Configuration,builder.Environment,"notifications");
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
        app.MapServiceOperations("notifications");
        app.MapDatabaseReadiness(BackendSecurity.Human);
        app.MapBackendBoundary("Notifications");

        app.MapNotifications();
        TourLab.Azure.Notifications.Receipts.ReceiptEndpoints.MapReceipts(app);
        await app.RunAsync();
    }
}
