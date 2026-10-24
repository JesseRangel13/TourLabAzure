using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using TourLab.Azure.Contracts;
using TourLab.Azure.Infrastructure.Messaging;
using TourLab.Azure.Reservations.Application;
using TourLab.Azure.Reservations.Dashboard;
using TourLab.Azure.Reservations.Persistence;
namespace TourLab.Azure.Tests;
[Collection("SQL Identity")]
public sealed class DashboardSqlTests
{
    internal static Reservation Row(string user)=>new(){Id=Guid.NewGuid(),UserId=user,SessionId=Guid.NewGuid(),QuoteId=Guid.NewGuid(),
        TourName="Immutable SQL snapshot",StartsAtUtc=DateTimeOffset.UtcNow.AddDays(2),CreatedAtUtc=DateTimeOffset.UtcNow,
        Participants=2,UnitAmountMinor=12345,TotalAmountMinor=24690};
    [SqlFact]
    public async Task CreationEverySagaTransitionAndCancellationCommitMonotonicSnapshotsWithPriceAndNameUnchanged()
    {
        await using var flow=new RecoverySqlTests.Flow();await flow.Init();await flow.Confirmed();await flow.Cancel("dashboard-"+Guid.NewGuid());
        await flow.Refund();await flow.Pass("payments","reservations","PaymentRefunded");await flow.Release();
        await using var db=BookingSqlTests.Reservations();
        var row=await db.Reservations.SingleAsync(x=>x.Id==flow.Id);
        var json=await db.Outbox.Where(x=>x.Type=="BookingSnapshotChanged"&&x.EffectKey.StartsWith("booking/"+flow.Id+"/")).Select(x=>x.EnvelopeJson).ToArrayAsync();
        var snapshots=json.Select(MessageCodec.Parse).Select(MessageCodec.Payload<BookingSnapshotChanged>).OrderBy(x=>x.BusinessVersion).ToArray();
        Assert.Equal(Enumerable.Range(1,checked((int)row.Version)).Select(x=>(long)x),snapshots.Select(x=>x.BusinessVersion));
        Assert.Equal("AwaitingAvailability",snapshots[0].Status);Assert.Equal("Cancelled",snapshots[^1].Status);
        Assert.Contains(snapshots,x=>x.Status=="Confirmed");Assert.Contains(snapshots,x=>x.Status=="CancellationPending");
        Assert.All(snapshots,s=>{Assert.Equal(row.UserId,s.UserId);Assert.Equal(row.TourName,s.TourNameSnapshot);Assert.Equal(24690,s.AmountCents);Assert.Equal(2,s.Participants);Assert.Equal(row.StartsAtUtc,s.DepartureUtc);});
        Assert.All(await db.Outbox.Where(x=>x.Type=="BookingSnapshotChanged"&&x.EffectKey.StartsWith("booking/"+flow.Id+"/")).ToArrayAsync(),x=>Assert.Equal(MessageRoutes.Topic,x.Destination));
    }
    [SqlFact]
    public async Task RollbackCannotLeaveAReservationOrProjectionIntent()
    {
        var row=Row("rollback-"+Guid.NewGuid());await using var db=BookingSqlTests.Reservations();
        await using(var tx=await db.Database.BeginTransactionAsync()){
            db.Reservations.Add(row);BookingSnapshots.Add(db,row,TimeProvider.System.GetUtcNow(),Guid.NewGuid());
            await db.SaveChangesAsync();await tx.RollbackAsync();
        }
        db.ChangeTracker.Clear();Assert.False(await db.Reservations.AnyAsync(x=>x.Id==row.Id));
        Assert.False(await db.Outbox.AnyAsync(x=>x.EffectKey=="booking/"+row.Id+"/1"));
    }
    [SqlFact]
    public async Task KeysetRebuildIsBoundedAuditedRepeatableAndDoesNotResetInboxOrDeleteData()
    {
        var user="rebuild-"+Guid.NewGuid();await using var db=BookingSqlTests.Reservations();
        var before=await db.Reservations.MaxAsync(x=>(long?)x.ProjectionSequence)??0;
        var rows=Enumerable.Range(0,3).Select(_=>Row(user)).ToArray();db.Reservations.AddRange(rows);await db.SaveChangesAsync();
        using var cursor=new DashboardCursor(new EphemeralDataProtectionProvider(),TimeProvider.System);
        var token=cursor.Protect(cursor.New("admin","rebuild-v1",2) with{After=before,Upper=rows.Max(x=>x.ProjectionSequence),Run=Guid.NewGuid()});
        var inbox=await db.Inbox.CountAsync();
        var rebuilder=new BookingRebuilder(db,cursor,TimeProvider.System);
        var first=await rebuilder.RebuildAsync("admin",new(2,token),default);Assert.Equal(2,first.Enqueued);Assert.NotNull(first.Cursor);
        Assert.Equal(0,(await rebuilder.RebuildAsync("admin",new(2,token),default)).Enqueued);
        var later=Row(user);db.Reservations.Add(later);await db.SaveChangesAsync();
        var last=await rebuilder.RebuildAsync("admin",new(2,first.Cursor),default);Assert.Equal(1,last.Enqueued);Assert.Null(last.Cursor);
        Assert.Equal(4,await db.Reservations.CountAsync(x=>x.UserId==user));Assert.Equal(inbox,await db.Inbox.CountAsync());
        Assert.Equal(3,await db.Outbox.CountAsync(x=>x.EffectKey.StartsWith("booking-rebuild/"+first.Run+"/")));
        Assert.True(await db.Set<OperationsAudit>().AnyAsync(x=>x.Actor=="admin"&&x.Action=="DashboardRebuildBatch"));
        await Assert.ThrowsAsync<ReservationProblem>(()=>rebuilder.RebuildAsync("other-admin",new(2,first.Cursor),default));
        await Assert.ThrowsAsync<ReservationProblem>(()=>rebuilder.RebuildAsync("admin",new(51),default));
    }
    [CosmosFact]
    public async Task ActualSqlRebuildSnapshotCannotOverwriteNewerCancellationInCosmos()
    {
        await using var flow=new RecoverySqlTests.Flow();await flow.Init();await flow.Confirmed();
        await using var db=BookingSqlTests.Reservations();var row=await db.Reservations.AsNoTracking().SingleAsync(x=>x.Id==flow.Id);
        using var cursor=new DashboardCursor(new EphemeralDataProtectionProvider(),TimeProvider.System);
        var token=cursor.Protect(cursor.New("admin","rebuild-v1",1) with{After=row.ProjectionSequence-1,Upper=row.ProjectionSequence,Run=Guid.NewGuid()});
        var rebuild=await new BookingRebuilder(db,cursor,TimeProvider.System).RebuildAsync("admin",new(1,token),default);
        var old=MessageCodec.Parse((await db.Outbox.SingleAsync(x=>x.EffectKey.StartsWith("booking-rebuild/"+rebuild.Run+"/"))).EnvelopeJson);
        await flow.Cancel("cosmos-rebuild-"+Guid.NewGuid());
        db.ChangeTracker.Clear();var current=await db.Reservations.AsNoTracking().SingleAsync(x=>x.Id==flow.Id);
        using var store=CosmosIntegrationTests.Store();await store.ApplyAsync(BookingSnapshots.From(current),default);
        await new DashboardConsumer(store).ConsumeAsync(old,default);
        Assert.Equal(current.Version,(await store.ReadAsync(current.UserId,current.Id,default))!.BusinessVersion);
        Assert.Equal("CancellationPending",(await store.ReadAsync(current.UserId,current.Id,default))!.Status);
    }
}
