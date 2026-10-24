using TourLab.Azure.Catalog.Images;
using TourLab.Azure.Infrastructure.Messaging;
using TourLab.Azure.Infrastructure.Operations;
using TourLab.Azure.Catalog.Messaging;
using TourLab.Azure.Catalog.Application;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TourLab.Azure.Infrastructure;
using TourLab.Azure.Infrastructure.Persistence;
using TourLab.Azure.Infrastructure.Security;
using TourLab.Azure.Catalog.Persistence;

namespace TourLab.Azure.Catalog;

public partial class Program
{
    public static async Task Main(string[] args)
    {
        var builder = ScaffoldHosting.Create(args);
        builder.Services.AddRuntimeDatabase("catalog");
        builder.Services.AddDbContext<CatalogDb>((services, options) => options.UseSqlServer(
            services.GetRequiredService<IOptions<RuntimeDatabase>>().Value.ConnectionString,
            sql => { sql.MigrationsHistoryTable("__EFMigrationsHistory", "catalog"); sql.CommandTimeout(5); }));
        builder.Services.AddBackendSecurity("Catalog");
        builder.Services.AddTourImages();

        builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.RespectRequiredConstructorParameters = true);
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddOpenApi();
        builder.Services.AddScoped<CatalogQueries>();
        builder.Services.AddScoped<CatalogCommands>();
        builder.Services.AddScoped<QuoteHandler>();
        builder.Services.AddScoped<InventoryHandlers>();
        builder.Services.AddHostedService<HoldExpiryWorker>();

        builder.Services.AddServiceOperations(builder.Configuration,builder.Environment,"catalog");
        builder.Services.Configure<MessagingOptions>(builder.Configuration.GetSection("Messaging"));
        builder.Services.AddLabMessaging("catalog");
        builder.Services.AddScoped<IInboxConsumer, CatalogConsumer>();
        builder.Services.ConfigureHttpJsonOptions(o=>o.SerializerOptions.RespectRequiredConstructorParameters=true);
        var app = builder.Build();
        app.MapScaffold("Catalog");
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapServiceOperations("catalog");
        app.MapDatabaseReadiness(BackendSecurity.Human);
        app.MapBackendBoundary("Catalog");
        app.MapCatalog(); app.MapTourImages();
        if (app.Environment.IsDevelopment())
            app.MapOpenApi("/v1/openapi/{documentName}.json").RequireAuthorization(BackendSecurity.Human);
        await app.RunAsync();
    }
}
