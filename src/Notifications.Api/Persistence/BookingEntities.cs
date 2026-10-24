namespace TourLab.Azure.Notifications.Persistence;
public sealed class Notification
{
    public Guid Id {get;set;}
    public Guid ReservationId {get;set;}
    public Guid SagaId {get;set;}
    public Guid SourceEventId {get;set;}
    public string UserId {get;set;} = "";
    public string Kind {get;set;} = "";
    public string Subject {get;set;} = "";
    public string Body {get;set;} = "";
    public string Mode {get;set;} = "None";
    public string Status {get;set;} = "Pending";
    public DateTimeOffset CreatedAtUtc {get;set;}
    public DateTimeOffset? SentAtUtc {get;set;}
    public DateTimeOffset NextAttemptAtUtc {get;set;}
    public Guid? LeaseOwner {get;set;}
    public DateTimeOffset? LeaseUntilUtc {get;set;}
    public long Version {get;set;} = 1;
}
public sealed class DeliveryReceipt
{
    public Guid Id {get;set;}
    public Guid SourceEventId {get;set;}
    public string Kind {get;set;} = "";
    public string RequestHash {get;set;} = "";
    public int Failures {get;set;}
    public DateTimeOffset? DeliveredAtUtc {get;set;}
}
public sealed class NotificationFault
{
    public int Id {get;set;} = 1;
    public string Mode {get;set;} = "None";
    public int Remaining {get;set;}
}
public sealed class NotificationAudit
{
    public Guid Id {get;set;}
    public string Actor {get;set;} = "";
    public string Action {get;set;} = "";
    public DateTimeOffset OccurredAtUtc {get;set;}
}
