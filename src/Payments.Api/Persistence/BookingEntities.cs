namespace TourLab.Azure.Payments.Persistence;
public sealed class PaymentOperation
{
    public Guid Id { get; set; }
    public Guid SagaId { get; set; }
    public Guid ReservationId { get; set; }
    public string UserId { get; set; } = "";
    public long AmountMinor { get; set; }
    public string Currency { get; set; } = "MXN";
    public string Status { get; set; } = "Pending";
    public string Mode { get; set; } = "Success";
    public string? ProviderReference { get; set; }
    public string? Reason { get; set; }
    public DateTimeOffset? NextReconcileAtUtc { get; set; }
    public int ReconcileAttempts { get; set; }
    public Guid SourceMessageId { get; set; }
    public Guid CorrelationId { get; set; }
    public Guid? LeaseOwner { get; set; }
    public DateTimeOffset? LeaseUntilUtc { get; set; }
    public long Version { get; set; } = 1;
}
public sealed class ProviderEffect
{
    public Guid Id { get; set; }
    public string RequestHash { get; set; } = "";
    public string Mode { get; set; } = "";
    public string Status { get; set; } = "";
    public string? Reference { get; set; }
    public bool TimeoutDelivered { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
}


public sealed class RefundOperation
{
    public Guid Id { get; set; }
    public Guid PaymentOperationId { get; set; }
    public Guid SagaId { get; set; }
    public Guid ReservationId { get; set; }
    public string UserId { get; set; } = "";
    public long AmountMinor { get; set; }
    public string Currency { get; set; } = "MXN";
    public string Mode { get; set; } = "Success";
    public string Status { get; set; } = "Pending";
    public int Attempts { get; set; }
    public long LastRetryVersion { get; set; }
    public DateTimeOffset? NextAttemptAtUtc { get; set; }
    public Guid? LeaseOwner { get; set; }
    public DateTimeOffset? LeaseUntilUtc { get; set; }
    public string? ProviderReference { get; set; }
    public string? Reason { get; set; }
    public Guid SourceMessageId { get; set; }
    public Guid CorrelationId { get; set; }
    public long Version { get; set; } = 1;
}

public sealed class ProviderRefund
{
    public Guid Id { get; set; }
    public Guid PaymentOperationId { get; set; }
    public string RequestHash { get; set; } = "";
    public int Failures { get; set; }
    public string? Reference { get; set; }
}
public sealed class PaymentFault
{
    public int Id {get;set;} = 1;
    public string Mode {get;set;} = "Success";
    public int Remaining {get;set;}
}
public sealed class PaymentAudit
{
    public Guid Id {get;set;}
    public string Actor {get;set;} = "";
    public string Action {get;set;} = "";
    public DateTimeOffset OccurredAtUtc {get;set;}
}
