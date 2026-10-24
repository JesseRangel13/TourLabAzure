namespace TourLab.Azure.Reservations.Persistence;
public sealed class Reservation
{
    public long ProjectionSequence { get; set; }
    public string TourName { get; set; } = "Tour name unavailable (legacy)";
    public Guid Id { get; set; }
    public string UserId { get; set; } = "";
    public Guid SessionId { get; set; }
    public int Participants { get; set; }
    public Guid QuoteId { get; set; }
    public long UnitAmountMinor { get; set; }
    public long TotalAmountMinor { get; set; }
    public string Currency { get; set; } = "MXN";
    public DateTimeOffset StartsAtUtc { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public string Status { get; set; } = "AwaitingAvailability";
    public string? Reason { get; set; }
    public long Version { get; set; } = 1;
}
public sealed class ReservationSaga
{
    public string? Traceparent {get;set;}
    public string? Tracestate {get;set;}
    public Guid Id { get; set; }
    public Guid ReservationId { get; set; }
    public Guid HoldId { get; set; }
    public Guid PaymentOperationId { get; set; }
    public Guid RefundOperationId { get; set; }
    public string State { get; set; } = "AwaitingAvailability";
    public DateTimeOffset DeadlineUtc { get; set; }
    public DateTimeOffset? HoldExpiresAtUtc { get; set; }
    public string? FailureReason { get; set; }
    public string? ProviderReference { get; set; }
    public bool RefundRequired { get; set; }
    public bool RefundCompleted { get; set; }
    public bool SeatsReleaseRequired { get; set; }
    public bool SeatsReleased { get; set; }
    public bool PaymentRequested { get; set; }
    public bool PaymentResolved { get; set; }
    public bool CancellationRequested { get; set; }
    public long Version { get; set; } = 1;
}
public sealed class IdempotencyRequest
{
    public string UserId { get; set; } = "";
    public string OperationName { get; set; } = "reservation-create";
    public string Key { get; set; } = "";
    public string RequestHash { get; set; } = "";
    public Guid ResourceId { get; set; }
    public int ResponseStatus { get; set; } = 202;
    public string ResponseBody { get; set; } = "";
    public string Location { get; set; } = "";
}
public sealed class TransitionHistory
{
    public Guid Id { get; set; }
    public Guid SagaId { get; set; }
    public Guid MessageId { get; set; }
    public string From { get; set; } = "";
    public string To { get; set; } = "";
    public string? Reason { get; set; }
    public DateTimeOffset OccurredAtUtc { get; set; }
}
public sealed class TourProjection
{
    public Guid SessionId { get; set; }
    public string Name { get; set; } = "";
    public bool Active { get; set; }
    public long PriceVersion { get; set; }
    public long UnitAmountMinor { get; set; }
    public string Currency { get; set; } = "MXN";
    public DateTimeOffset UpdatedAtUtc { get; set; }
}
