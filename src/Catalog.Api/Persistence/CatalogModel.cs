using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace TourLab.Azure.Catalog.Persistence;

internal sealed class CatalogUtcConverter() : ValueConverter<DateTimeOffset, DateTime>(
    value => value.UtcDateTime, value => new DateTimeOffset(DateTime.SpecifyKind(value, DateTimeKind.Utc)));

internal static class CatalogModel
{
    public static void Configure(ModelBuilder model)
    {
        model.Entity<Tour>(b =>
        {
            b.ToTable("Tours", t => t.HasCheckConstraint("CK_Tour", "LEN(LTRIM(RTRIM([Name])))>0 AND [Version]>0"));
            b.HasKey(x => x.Id);
            b.Property(x => x.Name).HasMaxLength(200);
            b.Property(x => x.Description).HasMaxLength(4000);
            b.Property(x => x.Version).IsConcurrencyToken();
            b.HasIndex(x => new { x.Active, x.Name, x.Id });
        });
        model.Entity<TourSession>(b =>
        {
            b.ToTable("Sessions", t => t.HasCheckConstraint("CK_Session", "[UnitAmountMinor]>0 AND [Currency]='MXN' AND [Capacity]>=0 AND [AvailableSeats]>=0 AND [AvailableSeats]<=[Capacity] AND [Version]>0 AND [PriceVersion]>0"));
            b.HasKey(x => x.Id);
            b.HasOne<Tour>().WithMany().HasForeignKey(x => x.TourId).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => new { x.TourId, x.StartsAtUtc });
            b.Property(x => x.Currency).HasMaxLength(3);
            b.Property(x => x.StartsAtUtc).HasConversion<CatalogUtcConverter>().HasColumnType("datetime2");
            b.Property(x => x.Version).IsConcurrencyToken();
        });
        model.Entity<Quote>(b =>
        {
            b.ToTable("Quotes", t => t.HasCheckConstraint("CK_Quote", "[Participants] BETWEEN 1 AND 6 AND [UnitAmountMinor]>0 AND [TotalAmountMinor]>0 AND [Currency]='MXN' AND LEN([UserId])>0 AND CONVERT(decimal(38,0),[TotalAmountMinor])=CONVERT(decimal(38,0),[UnitAmountMinor])*[Participants]"));
            b.HasKey(x => x.Id);
            b.HasIndex(x => x.RequestId).IsUnique();
            b.HasIndex(x => x.ExpiresAtUtc);
            b.Property(x => x.UserId).HasMaxLength(128);
            b.Property(x => x.Currency).HasMaxLength(3);
            b.Property(x => x.TourName).HasMaxLength(200);
            b.Property(x => x.ExpiresAtUtc).HasConversion<CatalogUtcConverter>().HasColumnType("datetime2");
            b.HasOne<TourSession>().WithMany().HasForeignKey(x => x.SessionId).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<SeatHold>(b =>
        {
            b.ToTable("Holds", t => t.HasCheckConstraint("CK_Hold", "[Version]>0 AND [Status] IN ('Held','Confirmed','Released','Expired','Rejected') AND (([SessionId] IS NULL AND [QuoteId] IS NULL AND [Participants]=0 AND [Status] IN ('Released','Rejected')) OR ([SessionId] IS NOT NULL AND [QuoteId] IS NOT NULL AND [ReservationId] IS NOT NULL AND [UserId] IS NOT NULL AND [Participants] BETWEEN 1 AND 6 AND [ExpiresAtUtc] IS NOT NULL))"));
            b.HasKey(x => x.Id);
            b.Property(x => x.Version).IsConcurrencyToken();
            b.Property(x => x.UserId).HasMaxLength(128);
            b.Property(x => x.Status).HasMaxLength(16);
            b.Property(x => x.Reason).HasMaxLength(100);
            b.Property(x => x.RequestFingerprint).HasMaxLength(64).IsFixedLength();
            b.Property(x => x.ExpiresAtUtc).HasConversion<CatalogUtcConverter>().HasColumnType("datetime2");
            b.Property(x => x.CreatedAtUtc).HasConversion<CatalogUtcConverter>().HasColumnType("datetime2");
            // SQL Server unique indexes need an explicit filter to permit many null tombstones.
            b.HasIndex(x => x.QuoteId).IsUnique().HasFilter("[QuoteId] IS NOT NULL");
            b.HasIndex(x => new { x.Status, x.ExpiresAtUtc });
            b.HasOne<TourSession>().WithMany().HasForeignKey(x => x.SessionId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<Quote>().WithMany().HasForeignKey(x => x.QuoteId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}