using Microsoft.EntityFrameworkCore;
namespace TourLab.Azure.Notifications.Persistence;
internal static class BookingModel { public static void Configure(ModelBuilder model) {
        model.Entity<Notification>(b=> {b.ToTable("Notifications"); b.HasKey(x=>x.Id); b.HasIndex(x=>new{x.ReservationId,x.Kind}).IsUnique();
            b.HasIndex(x=>new{x.UserId,x.CreatedAtUtc}); b.HasIndex(x=>new{x.Status,x.NextAttemptAtUtc,x.LeaseUntilUtc}); b.Property(x=>x.Version).IsConcurrencyToken();});
        model.Entity<DeliveryReceipt>(b=> {b.ToTable("DeliveryReceipts"); b.HasKey(x=>x.Id); b.HasIndex(x=>new{x.SourceEventId,x.Kind}).IsUnique();});
        model.Entity<NotificationFault>(b => { b.ToTable("Faults"); b.HasKey(x=>x.Id); b.HasData(new NotificationFault()); });
model.Entity<Notification>().Property(x=>x.UserId).HasMaxLength(128); model.Entity<Notification>().Property(x=>x.Kind).HasMaxLength(40); model.Entity<DeliveryReceipt>().Property(x=>x.Kind).HasMaxLength(40);
        model.Entity<Notification>().Property(x=>x.Traceparent).HasMaxLength(512); model.Entity<Notification>().Property(x=>x.Tracestate).HasMaxLength(512);
} }