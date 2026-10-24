using TourLab.Azure.Reservations.Application;
using TourLab.Azure.Infrastructure.Messaging;
using TourLab.Azure.Infrastructure.Operations;
using TourLab.Azure.Reservations.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TourLab.Azure.Infrastructure;
using TourLab.Azure.Infrastructure.Persistence;
using TourLab.Azure.Infrastructure.Security;
using TourLab.Azure.Reservations.Persistence;

namespace TourLab.Azure.Reservations;

public partial class Program
{
    public static async Task Main(string[] args)
    {
        var builder = ScaffoldHosting.Create(args);
        builder.Services.AddRuntimeDatabase("reservations");
        builder.Services.AddDbContext<ReservationsDb>((services, options) => options.UseSqlServer(
            services.GetRequiredService<IOptions<RuntimeDatabase>>().Value.ConnectionString,
            sql => { sql.MigrationsHistoryTable("__EFMigrationsHistory", "reservations"); sql.CommandTimeout(5); }));
        builder.Services.AddBackendSecurity("Reservations");
        builder.Services.AddSingleton<QuoteServiceTokens>();
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddServiceOperations(builder.Configuration,builder.Environment,"reservations");
        builder.Services.Configure<MessagingOptions>(builder.Configuration.GetSection("Messaging"));
        builder.Services.AddLabMessaging("reservations");
        builder.Services.AddScoped<IInboxConsumer, ReservationConsumer>();
        builder.Services.AddScoped<ReservationCreator>();
        builder.Services.AddScoped<SagaCommands>();
        builder.Services.AddScoped<ReservationQueries>();
        builder.Services.AddSingleton<QuoteConcurrencyLimit>();
        builder.Services.Configure<CatalogClientOptions>(o => {
            o.BaseAddress=builder.Configuration["Catalog:BaseAddress"]??"https://localhost:9444/";
            if(!Uri.TryCreate(o.BaseAddress,UriKind.Absolute,out var uri)||uri.Scheme!="https"||uri.Host is not ("localhost" or "127.0.0.1")||uri.Port!=9444)
                throw new InvalidOperationException("Only the isolated HTTPS Catalog is permitted in the local profile.");
            o.TotalTimeoutSeconds=10;
        });
        builder.Services.AddHttpClient<ICatalogQuoteClient,CatalogQuoteClient>((sp,client)=>{
            client.BaseAddress=new Uri(sp.GetRequiredService<IOptions<CatalogClientOptions>>().Value.BaseAddress);
            client.Timeout=Timeout.InfiniteTimeSpan;
        }).ConfigurePrimaryHttpMessageHandler(()=>QuoteHttpSetup.Handler(builder.Configuration,builder.Environment));
        builder.Services.Configure<SagaRecoveryOptions>(o=>o.Enabled=builder.Configuration.GetValue<bool>("Messaging:Enabled"));
        builder.Services.AddSingleton(sp=>sp.GetRequiredService<IOptions<SagaRecoveryOptions>>().Value);
        builder.Services.AddScoped<SagaRecovery>();
        builder.Services.AddHostedService<SagaDeadlineWorker>();
        builder.Services.ConfigureHttpJsonOptions(o=>o.SerializerOptions.RespectRequiredConstructorParameters=true);
        var app = builder.Build();
        app.MapScaffold("Reservations");
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapServiceOperations("reservations");
        app.MapDatabaseReadiness(BackendSecurity.Human);
        app.MapBackendBoundary("Reservations");

        app.MapReservations();
        await app.RunAsync();
    }
}
