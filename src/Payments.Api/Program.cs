using TourLab.Azure.Payments.Application;
using TourLab.Azure.Infrastructure.Messaging;
using TourLab.Azure.Infrastructure.Operations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TourLab.Azure.Infrastructure;
using TourLab.Azure.Infrastructure.Persistence;
using TourLab.Azure.Infrastructure.Security;
using TourLab.Azure.Payments.Persistence;

namespace TourLab.Azure.Payments;

public partial class Program
{
    public static async Task Main(string[] args)
    {
        var builder = await ScaffoldHosting.CreateAsync(args, "payments");
        builder.Services.AddRuntimeDatabase("payments");
        builder.Services.AddDbContextFactory<PaymentsDb>((services, options) => options.UseSqlServer(
            services.GetRequiredService<IOptions<RuntimeDatabase>>().Value.ConnectionString,
            sql => { sql.MigrationsHistoryTable("__EFMigrationsHistory", "payments"); sql.CommandTimeout(5); }), ServiceLifetime.Scoped);
        builder.Services.AddBackendSecurity("Payments");

        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddServiceOperations(builder.Configuration,builder.Environment,"payments");
        builder.Services.Configure<MessagingOptions>(builder.Configuration.GetSection("Messaging"));
        builder.Services.AddLabMessaging("payments");
        builder.Services.AddScoped<IInboxConsumer, PaymentConsumer>();
        builder.Services.AddScoped<IPaymentProvider,DurableFakeProvider>();
        builder.Services.AddScoped<PaymentProcessor>();
        builder.Services.AddScoped<RefundProcessor>();
        builder.Services.Configure<PaymentWorkOptions>(o=>o.Enabled=builder.Configuration.GetValue<bool>("Messaging:Enabled"));
        builder.Services.AddHostedService<PaymentWorker>();
        builder.Services.ConfigureHttpJsonOptions(o=>o.SerializerOptions.RespectRequiredConstructorParameters=true);
        var app = builder.Build();
        app.MapScaffold("Payments");
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapServiceOperations("payments");
        app.MapDatabaseReadiness(BackendSecurity.Human);
        app.MapBackendBoundary("Payments");

        app.MapPayments();
        await app.RunAsync();
    }
}
