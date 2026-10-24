using System.Text.Json.Serialization;
namespace TourLab.Azure.Contracts;
public sealed record HoldSeats(
    Guid HoldId,
    Guid QuoteId,
    Guid ReservationId,
    string UserId);

public sealed record ConfirmSeats(
    Guid HoldId);

public sealed record ReleaseSeats(
    Guid HoldId,
    string Reason);

public sealed record SeatsHeld(
    Guid HoldId,
    DateTimeOffset ExpiresAtUtc);

public sealed record SeatsHoldRejected(
    Guid HoldId,
    string Reason);

public sealed record SeatsConfirmed(
    Guid HoldId);

public sealed record SeatsConfirmationRejected(
    Guid HoldId,
    string Reason);

public sealed record SeatsHoldExpired(
    Guid HoldId);

public sealed record SeatsReleased(
    Guid HoldId);

public sealed record TourSessionChanged(
    Guid SessionId,
    string TourName,
    bool Active,
    long PriceVersion,
    long UnitAmountMinor,
    string Currency);

public sealed record IntegrationEnvelope<T>(
    Guid MessageId,
    Guid DeliveryId,
    string Type,
    int Version,
    DateTimeOffset OccurredAtUtc,
    Guid? SagaId,
    Guid CorrelationId,
    Guid CausationId,
    string? Traceparent,
    T Payload)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Tracestate { get; init; }
}
