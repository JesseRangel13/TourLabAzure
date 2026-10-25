using Microsoft.EntityFrameworkCore;
namespace TourLab.Azure.Notifications.Receipts;

public sealed class ReceiptJob
{
    public Guid Id { get; set; }
    public Guid ReservationId { get; set; }
    public Guid SagaId { get; set; }
    public string UserId { get; set; } = "";
    public string Kind { get; set; } = "BookingConfirmation";
    public long SourceBusinessVersion { get; set; }
    public string SnapshotJson { get; set; } = "";
    public string ContentHash { get; set; } = "";
    public string BlobKey { get; set; } = "";
    public string Status { get; set; } = "Pending";
    public string? ErrorCode { get; set; }
    public DateTimeOffset ConfirmedAtUtc { get; set; }
    public DateTimeOffset? ReadyAtUtc { get; set; }
    public Guid? LeaseOwner { get; set; }
    public DateTimeOffset? LeaseUntilUtc { get; set; }
    public int Attempts { get; set; }
    public long Version { get; set; } = 1;
}
public sealed class ReceiptAttempt
{
    public Guid Id { get; set; }
    public Guid JobId { get; set; }
    public string Outcome { get; set; } = "";
    public DateTimeOffset OccurredAtUtc { get; set; }
}
internal static class ReceiptModel
{
    public static void Configure(ModelBuilder model)
    {
        model.Entity<ReceiptJob>(b => {
            b.ToTable("ReceiptJobs", t => {
                t.HasCheckConstraint("CK_ReceiptJobs_Json", "ISJSON([SnapshotJson])=1");
                t.HasCheckConstraint("CK_ReceiptJobs_Status", "[Status] IN ('Pending','Ready','Conflict')");
                t.HasCheckConstraint("CK_ReceiptJobs_Version", "[SourceBusinessVersion]>0 AND [Version]>0");
            });
            b.HasKey(x => x.Id);
            b.HasIndex(x => new { x.ReservationId, x.Kind, x.SourceBusinessVersion }).IsUnique();
            b.Property(x => x.UserId).HasMaxLength(128); b.Property(x => x.Kind).HasMaxLength(40);
            b.Property(x => x.BlobKey).HasMaxLength(200); b.Property(x => x.ContentHash).HasMaxLength(64);
            b.Property(x => x.Status).HasMaxLength(20); b.Property(x => x.ErrorCode).HasMaxLength(80);
            b.Property(x => x.Version).IsConcurrencyToken();
        });
        model.Entity<ReceiptAttempt>(b => {
            b.ToTable("ReceiptAttempts"); b.HasKey(x => x.Id);
            b.HasIndex(x => new { x.JobId, x.OccurredAtUtc }); b.Property(x => x.Outcome).HasMaxLength(80);
        });
    }
}