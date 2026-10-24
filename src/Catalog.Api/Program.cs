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

        var app = builder.Build();
        app.MapScaffold("Catalog");
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapDatabaseReadiness(BackendSecurity.Human);
        app.MapBackendBoundary("Catalog");
        app.MapPost("/v1/internal/quotes", () => Results.Problem(statusCode: 503,
            title: "Catalog quote workflow is scheduled in AZ-003",
            extensions: new Dictionary<string, object?> { ["code"] = "QuoteWorkflowNotImplemented" }))
            .RequireAuthorization(BackendSecurity.Quote);
        await app.RunAsync();
    }
}
