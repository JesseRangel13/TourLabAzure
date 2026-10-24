using Microsoft.EntityFrameworkCore;
namespace TourLab.Azure.Catalog.Persistence;

public sealed class TourImage
{
    public Guid Id { get; set; }
    public Guid TourId { get; set; }
    public string ObjectKey { get; set; } = "";
    public string ContentType { get; set; } = "";
    public long ByteSize { get; set; }
    public string Sha256 { get; set; } = "";
    public string Status { get; set; } = "Pending";
    public DateTimeOffset CreatedAtUtc { get; set; }
    public Guid? LeaseOwner { get; set; }
    public DateTimeOffset? LeaseUntilUtc { get; set; }
    public long Version { get; set; } = 1;
    public int Attempts { get; set; }
}
public sealed class ImageHistory
{
    public long Id { get; set; }
    public Guid ImageId { get; set; }
    public DateTimeOffset AtUtc { get; set; }
    public string Outcome { get; set; } = "";
}
public static class ImageModel
{
    public static void Configure(ModelBuilder model)
    {
        model.Entity<TourImage>(b =>
        {
            b.ToTable("TourImages", t => t.HasCheckConstraint("CK_TourImage", "[Status] IN ('Pending','Ready','DeletePending','Deleted') AND [ByteSize] BETWEEN 1 AND 2097152 AND [Version]>0 AND [Attempts]>=0"));
            b.HasKey(x => x.Id);
            b.HasOne<Tour>().WithMany().HasForeignKey(x => x.TourId).OnDelete(DeleteBehavior.Restrict);
            b.Property(x => x.ObjectKey).HasMaxLength(120);
            b.HasIndex(x => x.ObjectKey).IsUnique();
            b.Property(x => x.ContentType).HasMaxLength(16);
            b.Property(x => x.Sha256).HasMaxLength(64).IsFixedLength();
            b.Property(x => x.Status).HasMaxLength(16);
            b.Property(x => x.Version).IsConcurrencyToken();
            b.Property(x => x.CreatedAtUtc).HasConversion<CatalogUtcConverter>().HasColumnType("datetime2");
            b.Property(x => x.LeaseUntilUtc).HasConversion<CatalogUtcConverter>().HasColumnType("datetime2");
            b.HasIndex(x => new { x.Status, x.LeaseUntilUtc, x.Id });
        });
        model.Entity<ImageHistory>(b =>
        {
            b.ToTable("ImageHistory"); b.HasKey(x => x.Id);
            b.Property(x => x.Outcome).HasMaxLength(40);
            b.Property(x => x.AtUtc).HasConversion<CatalogUtcConverter>().HasColumnType("datetime2");
            b.HasIndex(x => new { x.ImageId, x.Id });
            b.HasOne<TourImage>().WithMany().HasForeignKey(x => x.ImageId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
