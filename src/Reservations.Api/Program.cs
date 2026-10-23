using TourLab.Azure.Infrastructure;

namespace TourLab.Azure.Reservations;

public partial class Program
{
    public static async Task Main(string[] args)
    {
        var builder = ScaffoldHosting.Create(args);
        var app = builder.Build();
        app.MapScaffold("Reservations");

        await app.RunAsync();
    }
}
