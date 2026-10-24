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
        var app = builder.Build();
        app.MapScaffold("Reservations");
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapDatabaseReadiness(BackendSecurity.Human);
        app.MapBackendBoundary("Reservations");

        await app.RunAsync();
    }
}
