namespace TourLab.Azure.Receipts;
public sealed class ReceiptBusSettings
{
    public bool Cloud { get; set; }
    public bool LocalTestBroker { get; set; }
    public string ConnectionString { get; set; } = "";
    public static bool Safe(string connection,bool development,bool localTestBroker=false) =>
        TourLab.Azure.Infrastructure.Messaging.LocalBrokerEndpoint.Safe(connection,development,localTestBroker);
}
