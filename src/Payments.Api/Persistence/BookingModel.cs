using Microsoft.EntityFrameworkCore;
namespace TourLab.Azure.Payments.Persistence;
internal static class BookingModel { public static void Configure(ModelBuilder model) {
        model.Entity<PaymentOperation>(b => { b.ToTable("Operations", t => t.HasCheckConstraint("CK_Payment", "[AmountMinor]>0 AND [Currency]='MXN' AND [Version]>0")); b.HasKey(x=>x.Id);
            b.Property(x=>x.Version).IsConcurrencyToken(); b.Property(x=>x.UserId).HasMaxLength(128); b.HasIndex(x=>new {x.UserId,x.ReservationId});
            b.HasIndex(x=>new {x.Status,x.NextReconcileAtUtc,x.LeaseUntilUtc}); });
        model.Entity<ProviderEffect>(b=> {b.ToTable("ProviderEffects"); b.HasKey(x=>x.Id); b.Property(x=>x.RequestHash).HasMaxLength(64);});
        model.Entity<RefundOperation>(b => { b.ToTable("RefundOperations", t => t.HasCheckConstraint("CK_Refund", "[AmountMinor]>0 AND [Currency]='MXN' AND [Version]>0")); b.HasKey(x=>x.Id); b.HasIndex(x=>x.PaymentOperationId).IsUnique(); b.Property(x=>x.Version).IsConcurrencyToken(); b.HasIndex(x=>new {x.Status,x.NextAttemptAtUtc}); });
        model.Entity<ProviderRefund>(b => { b.ToTable("ProviderRefunds"); b.HasKey(x=>x.Id); b.HasIndex(x=>x.PaymentOperationId).IsUnique(); });
        model.Entity<PaymentFault>(b => { b.ToTable("Faults"); b.HasKey(x=>x.Id); b.HasData(new PaymentFault()); });

        model.Entity<PaymentOperation>().Property(x=>x.Traceparent).HasMaxLength(512); model.Entity<PaymentOperation>().Property(x=>x.Tracestate).HasMaxLength(512);
        model.Entity<RefundOperation>().Property(x=>x.Traceparent).HasMaxLength(512); model.Entity<RefundOperation>().Property(x=>x.Tracestate).HasMaxLength(512);
} }