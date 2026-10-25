using Azure.Messaging.ServiceBus;
namespace TourLab.Azure.Infrastructure.Messaging;
public static class LocalBrokerEndpoint
{
    public static bool Safe(string connection,bool development,bool isolatedTest=false)
    {
        try {
            var settings=ServiceBusConnectionStringProperties.Parse(connection);
            return development && settings.Endpoint.Host is "localhost" or "127.0.0.1" &&
                settings.Endpoint.Port==(isolatedTest?5674:5673) &&
                connection.Contains("UseDevelopmentEmulator=true",StringComparison.OrdinalIgnoreCase);
        }catch(Exception error) when(error is ArgumentException or FormatException){return false;}
    }
}
