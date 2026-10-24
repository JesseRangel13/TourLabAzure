namespace TourLab.Azure.Contracts;

// Full immutable booking snapshot; no consumer-side Catalog or SQL lookup is required.
public sealed record BookingSnapshotChanged(
    string UserId, Guid ReservationId, string TourNameSnapshot, DateTimeOffset DepartureUtc,
    int Participants, long AmountCents, string Currency, string Status, long BusinessVersion);
