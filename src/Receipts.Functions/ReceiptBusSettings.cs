using Azure.Messaging.ServiceBus;
namespace TourLab.Azure.Receipts;
public sealed class ReceiptBusSettings
{
    public bool Cloud { get; set; }
    public string ConnectionString { get; set; } = "";
    public static bool Safe(string connection, bool development)
    {
        try {
            var settings = ServiceBusConnectionStringProperties.Parse(connection);
            return development && settings.Endpoint.Host is "localhost" or "127.0.0.1" &&
                settings.Endpoint.Port == 5673 && connection.Contains("UseDevelopmentEmulator=true", StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception e) when (e is ArgumentException or FormatException) { return false; }
    }
}