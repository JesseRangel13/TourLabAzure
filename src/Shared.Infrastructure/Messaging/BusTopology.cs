namespace TourLab.Azure.Infrastructure.Messaging;
public static class BusTopology
{
    public static readonly string[] Commands = ["HoldSeats","ConfirmSeats","ReleaseSeats","ProcessPayment","ReconcilePayment","RefundPayment","GenerateBookingReceipt"];
    public static readonly IReadOnlyDictionary<string, string[]> Subscriptions = new Dictionary<string, string[]>
    {
        ["catalog-events"] = [],
        ["reservations-events"] = ["SeatsHeld","SeatsHoldRejected","SeatsConfirmed","SeatsConfirmationRejected","SeatsHoldExpired","SeatsReleased",
            "TourSessionChanged","PaymentSucceeded","PaymentDeclined","PaymentOutcomeUnknown","PaymentRefunded","RefundNeedsReview"],
        ["payments-events"] = [],
        ["notifications-events"] = ["ReservationConfirmed","ReservationFailed","ReservationCancelled"],
        ["dashboard-events"] = ["BookingSnapshotChanged"]
    };
    public static string CommandQueue(string schema) => "tourlab-azure-" + schema + "-commands";
    public static bool Accepts(string schema, bool events, string type) =>
        events ? Subscriptions[schema + "-events"].Contains(type, StringComparer.Ordinal)
        : MessageRoutes.Destinations(type).Single() == CommandQueue(schema);
}
public sealed class MessagingOptions
{
    public bool Enabled { get; set; }
    public string ConnectionString { get; set; } = "";
}