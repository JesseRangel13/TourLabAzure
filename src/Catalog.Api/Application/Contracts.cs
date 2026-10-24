using System.Text.Json.Serialization;

namespace TourLab.Azure.Catalog.Application;

public sealed record TourView(
    Guid Id,
    string Name,
    string Description,
    bool Active,
    long Version);

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

public sealed record TourPage(
    TourView[] Items,
    int Total,
    int Page,
    int PageSize);

public sealed record CreateTour(
    string? Name,
    string? Description);

public sealed record UpdateTour(
    string? Name,
    string? Description,
    bool Active,
    long ExpectedVersion);

public sealed record CreateSession(
    DateTimeOffset StartsAtUtc,
    long UnitAmountMinor,
    int Capacity);

public sealed record UpdatePrice(
    long UnitAmountMinor,
    long ExpectedVersion);

public sealed record UpdateCapacity(
    int Capacity,
    long ExpectedVersion);

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

