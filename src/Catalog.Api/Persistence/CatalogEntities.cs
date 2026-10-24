namespace TourLab.Azure.Catalog.Persistence;
public sealed class Tour
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public bool Active { get; set; } = true;
    public long Version { get; set; } = 1;
}

public sealed class TourSession
{
    public Guid Id { get; set; }
    public Guid TourId { get; set; }
    public DateTimeOffset StartsAtUtc { get; set; }
    public long UnitAmountMinor { get; set; }
    public string Currency { get; set; } = "MXN";
    public int Capacity { get; set; }
    public int AvailableSeats { get; set; }
    public long PriceVersion { get; set; } = 1;
    public long Version { get; set; } = 1;
}

public sealed class Quote
{
    public string TourName { get; set; } = "Tour name unavailable (legacy)";
    public Guid Id { get; set; }
    public Guid RequestId { get; set; }
    public Guid SessionId { get; set; }
    public int Participants { get; set; }
    public string UserId { get; set; } = "";
    public long UnitAmountMinor { get; set; }
    public long TotalAmountMinor { get; set; }
    public string Currency { get; set; } = "MXN";
    public DateTimeOffset ExpiresAtUtc { get; set; }
}

public sealed class SeatHold
{
    public Guid Id { get; set; }
    public Guid SagaId { get; set; }
    public Guid? ReservationId { get; set; }
    public Guid? SessionId { get; set; }
    public Guid? QuoteId { get; set; }
    public string? UserId { get; set; }
    public int Participants { get; set; }
    public string Status { get; set; } = "Held";
    public string? Reason { get; set; }
    public string? RequestFingerprint { get; set; }
    public DateTimeOffset? ExpiresAtUtc { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public long Version { get; set; } = 1;
}

