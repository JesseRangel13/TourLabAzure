using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TourLab.Azure.Contracts;
using TourLab.Azure.Infrastructure.Messaging;
using TourLab.Azure.Notifications.Persistence;
namespace TourLab.Azure.Notifications.Receipts;

public static class ReceiptDocument
{
    public const int MaximumBytes = 16384;
    public static void Validate(ConfirmationSnapshot s)
    {
        if (s.SourceBusinessVersion <= 0 || s.ConfirmedAtUtc == default || s.ConfirmedAtUtc.Offset != TimeSpan.Zero ||
            s.DepartureUtc == default || s.DepartureUtc.Offset != TimeSpan.Zero ||
            string.IsNullOrWhiteSpace(s.TourName) || s.TourName.Length > 200 ||
            s.Participants is < 1 or > 6 || s.AmountMinor <= 0 || s.Currency != "MXN")
            throw new PoisonMessageException("InvalidConfirmationSnapshot");
    }
    public static byte[] Render(Guid reservationId, ConfirmationSnapshot s)
    {
        Validate(s);
        return Encoding.UTF8.GetBytes(FormattableString.Invariant(
            $"SIMULATED EDUCATIONAL BOOKING CONFIRMATION\nNot a legal tax invoice or actual payment receipt.\nReservation: {reservationId:D}\nTour: {JsonSerializer.Serialize(s.TourName)}\nDeparture UTC: {s.DepartureUtc:O}\nParticipants: {s.Participants}\nAccepted amount: {s.AmountMinor} MXN cents\nConfirmed UTC: {s.ConfirmedAtUtc:O}\nSource business version: {s.SourceBusinessVersion}\nThis historical confirmation does not describe the current booking status.\n"));
    }
    public static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));
    public static string Key(Guid reservation, long version) => $"confirmations/{reservation:D}/v{version.ToString(CultureInfo.InvariantCulture)}.txt";
}
public static class ReceiptAcceptance
{
    // Called only inside Notifications' Inbox/effects/Outbox transaction and reservation lock.
    public static async Task AcceptAsync(NotificationsDb db, ReservationConfirmed confirmation,
        IntegrationEnvelope<JsonElement> source, CancellationToken ct)
    {
        // Version-1 historical events did not carry a snapshot. Never invent accepted prices or confirmation times.
        if (confirmation.Confirmation is not { } snapshot)
        {
            if (!await db.Notifications.AnyAsync(x => x.ReservationId == confirmation.ReservationId && x.Kind == nameof(ReservationConfirmed), ct))
                db.Audit.Add(new() { Id = Guid.NewGuid(), Actor = "ReceiptAcceptance", Action = "LegacyConfirmationWithoutSnapshot/" + confirmation.ReservationId, OccurredAtUtc = source.OccurredAtUtc });
            return;
        }
        ReceiptDocument.Validate(snapshot);
        var bytes = ReceiptDocument.Render(confirmation.ReservationId, snapshot);
        var json = JsonSerializer.Serialize(snapshot, MessageCodec.Json);
        var existing = await db.ReceiptJobs.SingleOrDefaultAsync(x => x.ReservationId == confirmation.ReservationId &&
            x.Kind == "BookingConfirmation" && x.SourceBusinessVersion == snapshot.SourceBusinessVersion, ct);
        if (existing is not null)
        {
            if (existing.UserId != confirmation.UserId || existing.SagaId != source.SagaId ||
                existing.SnapshotJson != json || existing.ContentHash != ReceiptDocument.Hash(bytes))
                throw new PoisonMessageException("ReceiptSnapshotConflict");
            return;
        }
        var job = new ReceiptJob {
            Id = Guid.NewGuid(), ReservationId = confirmation.ReservationId, UserId = confirmation.UserId,
            SagaId = source.SagaId!.Value, SourceBusinessVersion = snapshot.SourceBusinessVersion,
            SnapshotJson = json, ContentHash = ReceiptDocument.Hash(bytes),
            BlobKey = ReceiptDocument.Key(confirmation.ReservationId, snapshot.SourceBusinessVersion),
            ConfirmedAtUtc = snapshot.ConfirmedAtUtc
        };
        db.ReceiptJobs.Add(job);
        var message = Guid.NewGuid(); var delivery = Guid.NewGuid();
        db.Outbox.Add(new OutboxEntry {
            DeliveryId = delivery, MessageId = message, Type = nameof(GenerateBookingReceipt),
            Destination = MessageRoutes.Destinations(nameof(GenerateBookingReceipt)).Single(),
            EffectKey = $"receipt/{job.Id:D}", OccurredAtUtc = source.OccurredAtUtc.UtcDateTime,
            EnvelopeJson = MessageRoutes.Serialize(new GenerateBookingReceipt(job.Id), message, delivery,
                source.OccurredAtUtc, source.SagaId, source.CorrelationId, source.MessageId)
        });
    }
}