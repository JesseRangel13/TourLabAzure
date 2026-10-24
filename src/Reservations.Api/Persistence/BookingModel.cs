using Microsoft.EntityFrameworkCore;
namespace TourLab.Azure.Reservations.Persistence;
internal static class BookingModel { public static void Configure(ModelBuilder model) {
        model.Entity<Reservation>(b =>
        {
            b.ToTable("Reservations", t => t.HasCheckConstraint("CK_Reservation", "[Participants] BETWEEN 1 AND 6 AND [UnitAmountMinor] > 0 AND CONVERT(decimal(38,0),[TotalAmountMinor]) = CONVERT(decimal(38,0),[UnitAmountMinor]) * [Participants] AND [Currency]='MXN' AND [Version]>0"));
            b.Property(x => x.ProjectionSequence).UseIdentityColumn(); b.HasIndex(x => x.ProjectionSequence).IsUnique();
            b.HasKey(x => x.Id); b.Property(x => x.UserId).HasMaxLength(128); b.Property(x => x.Currency).HasMaxLength(3);
            b.Property(x => x.TourName).HasMaxLength(200);
            b.Property(x => x.Status).HasMaxLength(40); b.Property(x => x.Reason).HasMaxLength(100); b.Property(x => x.Version).IsConcurrencyToken();
            b.HasIndex(x => new { x.UserId, x.CreatedAtUtc }); b.HasIndex(x => x.Status);
        });
        model.Entity<ReservationSaga>(b =>
        {
            b.ToTable("Sagas", t => t.HasCheckConstraint("CK_Saga", "[Version]>0")); b.HasKey(x => x.Id);
            b.HasIndex(x => x.ReservationId).IsUnique(); b.HasIndex(x => x.HoldId).IsUnique(); b.HasIndex(x => x.PaymentOperationId).IsUnique(); b.HasIndex(x => x.RefundOperationId).IsUnique();
            b.HasIndex(x => new { x.State, x.DeadlineUtc }); b.Property(x => x.State).HasMaxLength(40); b.Property(x => x.FailureReason).HasMaxLength(100); b.Property(x => x.Version).IsConcurrencyToken();
            b.HasOne<Reservation>().WithMany().HasForeignKey(x => x.ReservationId).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<IdempotencyRequest>(b =>
        {
            b.ToTable("IdempotencyRequests"); b.HasKey(x => new { x.UserId, x.OperationName, x.Key });
            b.Property(x => x.UserId).HasMaxLength(128); b.Property(x => x.Key).HasMaxLength(128); b.Property(x => x.OperationName).HasMaxLength(40);
            b.Property(x => x.RequestHash).HasMaxLength(64); b.HasOne<Reservation>().WithMany().HasForeignKey(x => x.ResourceId).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<TransitionHistory>(b => { b.ToTable("History"); b.HasKey(x => x.Id); b.HasIndex(x => new { x.SagaId, x.MessageId }).IsUnique();
            b.Property(x => x.From).HasMaxLength(40); b.Property(x => x.To).HasMaxLength(40); b.Property(x => x.Reason).HasMaxLength(100); b.HasIndex(x => new { x.SagaId, x.OccurredAtUtc });
            b.HasOne<ReservationSaga>().WithMany().HasForeignKey(x => x.SagaId).OnDelete(DeleteBehavior.Restrict); });
        model.Entity<TourProjection>(b => { b.ToTable("TourProjections"); b.HasKey(x => x.SessionId); b.Property(x => x.Name).HasMaxLength(200); b.Property(x => x.Currency).HasMaxLength(3); });

        model.Entity<ReservationSaga>().Property(x=>x.Traceparent).HasMaxLength(512); model.Entity<ReservationSaga>().Property(x=>x.Tracestate).HasMaxLength(512);
} }
