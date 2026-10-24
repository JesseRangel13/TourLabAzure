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
        var builder = ScaffoldHosting.Create(args);
        builder.Services.AddRuntimeDatabase("payments");
        builder.Services.AddDbContext<PaymentsDb>((services, options) => options.UseSqlServer(
            services.GetRequiredService<IOptions<RuntimeDatabase>>().Value.ConnectionString,
            sql => { sql.MigrationsHistoryTable("__EFMigrationsHistory", "payments"); sql.CommandTimeout(5); }));
        builder.Services.AddBackendSecurity("Payments");

        var app = builder.Build();
        app.MapScaffold("Payments");
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapDatabaseReadiness(BackendSecurity.Human);
        app.MapBackendBoundary("Payments");

        await app.RunAsync();
    }
}
