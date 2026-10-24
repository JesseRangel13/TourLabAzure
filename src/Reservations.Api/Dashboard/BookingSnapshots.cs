using TourLab.Azure.Contracts;
using TourLab.Azure.Reservations.Application;
using TourLab.Azure.Reservations.Persistence;
namespace TourLab.Azure.Reservations.Dashboard;

public static class BookingSnapshots
{
    public static BookingSnapshotChanged From(Reservation row) => new(row.UserId, row.Id, row.TourName,
        row.StartsAtUtc.ToUniversalTime(), row.Participants, row.TotalAmountMinor, row.Currency, row.Status, row.Version);
    public static void Add(ReservationsDb db, Reservation row, DateTimeOffset now, Guid cause, string? key = null) =>
        ReservationIntents.Add(db, From(row), key ?? $"booking/{row.Id}/{row.Version}", now, null, row.Id, cause);
}
