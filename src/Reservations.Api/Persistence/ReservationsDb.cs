using TourLab.Azure.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace TourLab.Azure.Reservations.Persistence;

// Service-owned transport entities. Domain models arrive with their assigned tasks.
public sealed class OutboxEntry
{
    public Guid DeliveryId { get; set; }
    public Guid MessageId { get; set; }
    public string Destination { get; set; } = "";
    public string EffectKey { get; set; } = "";
    public string Type { get; set; } = "";
    public string EnvelopeJson { get; set; } = "";
    public DateTime OccurredAtUtc { get; set; }
    public DateTime? PublishedAtUtc { get; set; }
    public Guid? LeaseOwner { get; set; }
    public DateTime? LeaseUntilUtc { get; set; }
    public int Attempts { get; set; }
    public byte[] RowVersion { get; set; } = [];
}

public sealed class InboxEntry
{
    public string ConsumerName { get; set; } = "";
    public Guid MessageId { get; set; }
    public string Fingerprint { get; set; } = "";
    public DateTime ProcessedAtUtc { get; set; }
}

public sealed class ReservationsDb(DbContextOptions<ReservationsDb> options) : DbContext(options)
{
    public DbSet<OutboxEntry> Outbox => Set<OutboxEntry>();
    public DbSet<InboxEntry> Inbox => Set<InboxEntry>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.HasDefaultSchema("reservations");
        model.UseCollation("Latin1_General_100_BIN2");
        model.Entity<OutboxEntry>(b =>
        {
            b.ToTable("Outbox", table => table.HasCheckConstraint("CK_Outbox_Json", "ISJSON([EnvelopeJson])=1"));
            b.HasKey(x => x.DeliveryId);
            b.Property(x => x.Destination).HasMaxLength(128);
            b.Property(x => x.EffectKey).HasMaxLength(200);
            b.Property(x => x.Type).HasMaxLength(100);
            b.Property(x => x.EnvelopeJson).HasColumnType("nvarchar(max)");
            b.Property(x => x.OccurredAtUtc).HasConversion<UtcDateTimeConverter>().HasColumnType("datetime2");
            b.Property(x => x.PublishedAtUtc).HasConversion<UtcDateTimeConverter>().HasColumnType("datetime2");
            b.Property(x => x.LeaseUntilUtc).HasConversion<UtcDateTimeConverter>().HasColumnType("datetime2");
            b.Property(x => x.RowVersion).IsRowVersion();
            b.HasIndex(x => new { x.Destination, x.EffectKey }).IsUnique();
            b.HasIndex(x => new { x.PublishedAtUtc, x.OccurredAtUtc });
        });
        model.Entity<InboxEntry>(b =>
        {
            b.ToTable("Inbox");
            b.HasKey(x => new { x.ConsumerName, x.MessageId });
            b.Property(x => x.ConsumerName).HasMaxLength(100);
            b.Property(x => x.Fingerprint).HasMaxLength(64).IsFixedLength();
            b.Property(x => x.ProcessedAtUtc).HasConversion<UtcDateTimeConverter>().HasColumnType("datetime2");
        });
    }
}

public sealed class ReservationsDesignFactory : IDesignTimeDbContextFactory<ReservationsDb>
{
    public ReservationsDb CreateDbContext(string[] args) => new(new DbContextOptionsBuilder<ReservationsDb>()
        .UseSqlServer("Server=127.0.0.1,14339;Database=tourlab_azure_design;Integrated Security=True;Encrypt=True",
            sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", "reservations")).Options);
}
