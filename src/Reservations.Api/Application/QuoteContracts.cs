namespace TourLab.Azure.Reservations.Application;
public sealed record SessionView(
    Guid Id,
    Guid TourId,
    DateTimeOffset StartsAtUtc,
    long UnitAmountMinor,
    string Currency,
    int Capacity,
    int AvailableSeats,
    long PriceVersion,
    long Version);
public sealed record CreateQuote(
    Guid RequestId,
    Guid SessionId,
    int Participants,
    string? UserId,
    long ExpectedUnitAmountMinor,
    string? ExpectedCurrency);

public sealed record QuoteView(
    Guid QuoteId,
    long UnitAmountMinor,
    long TotalAmountMinor,
    string Currency,
    DateTimeOffset ExpiresAtUtc,
    SessionView Session,
    string TourName = "Tour name unavailable (legacy)");

