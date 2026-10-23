using TourLab.Azure.Infrastructure;

namespace TourLab.Azure.Notifications;

public partial class Program
{
    public static async Task Main(string[] args)
    {
        var builder = ScaffoldHosting.Create(args);
        var app = builder.Build();
        app.MapScaffold("Notifications");

        await app.RunAsync();
    }
}
