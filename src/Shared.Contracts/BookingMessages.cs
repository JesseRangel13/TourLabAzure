namespace TourLab.Azure.Contracts;
public sealed record ProcessPayment(
    Guid PaymentOperationId,
    Guid ReservationId,
    string UserId,
    long AmountMinor,
    string Currency);

public sealed record ReservationFailed(
    Guid ReservationId,
    string UserId,
    string Reason);


public sealed record PaymentSucceeded(Guid PaymentOperationId, string ProviderReference);
public sealed record PaymentDeclined(Guid PaymentOperationId, string Reason);
public sealed record PaymentOutcomeUnknown(Guid PaymentOperationId, DateTimeOffset NextReconcileAtUtc);
public sealed record ReconcilePayment(Guid PaymentOperationId);
public sealed record RefundPayment(Guid RefundOperationId, Guid PaymentOperationId, Guid ReservationId, string UserId, long AmountMinor, string Currency, long RetryVersion=0);
public sealed record PaymentRefunded(Guid RefundOperationId, Guid PaymentOperationId);
public sealed record RefundNeedsReview(Guid RefundOperationId, string Reason);
public sealed record ReservationCancelled(Guid ReservationId, string UserId, string Summary);
public sealed record ReservationConfirmed(Guid ReservationId, string UserId, string Summary);