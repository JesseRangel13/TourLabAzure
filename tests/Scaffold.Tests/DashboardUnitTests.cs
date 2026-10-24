using Microsoft.AspNetCore.DataProtection;
using TourLab.Azure.Contracts;
using TourLab.Azure.Infrastructure.Messaging;
using TourLab.Azure.Reservations.Application;
using TourLab.Azure.Reservations.Dashboard;
namespace TourLab.Azure.Tests;
public sealed class DashboardUnitTests
{
    private sealed class Clock:TimeProvider{public DateTimeOffset Now=DateTimeOffset.UtcNow;public override DateTimeOffset GetUtcNow()=>Now;}
    [Fact]
    public void CursorIsEncryptedBoundToUserQuerySizeAndExpiryAndRejectsTampering()
    {
        var clock=new Clock();using var cursor=new DashboardCursor(new EphemeralDataProtectionProvider(),clock);
        var state=cursor.New("alice","departure-desc-id-asc-v1",10) with{Token="opaque-provider-token"};
        var encoded=cursor.Protect(state);Assert.DoesNotContain("alice",encoded);Assert.DoesNotContain(state.Token,encoded);
        Assert.Equal(state,cursor.Read(encoded,"alice","departure-desc-id-asc-v1",10));
        Assert.Throws<ReservationProblem>(()=>cursor.Read(encoded,"bob",state.Query,10));
        Assert.Throws<ReservationProblem>(()=>cursor.Read(encoded,"alice","rebuild-v1",10));
        Assert.Throws<ReservationProblem>(()=>cursor.Read(encoded,"alice",state.Query,20));
        Assert.Throws<ReservationProblem>(()=>cursor.Read(encoded[..^10]+"wrong", "alice",state.Query,10));
        Assert.Throws<ReservationProblem>(()=>cursor.Read(new string('x',4097),"alice",state.Query,10));
        clock.Now=clock.Now.AddMinutes(16);Assert.Throws<ReservationProblem>(()=>cursor.Read(encoded,"alice",state.Query,10));
    }
    [Fact]
    public void SnapshotValidationAndCanonicalDatePreserveImmutableAcceptedFields()
    {
        var s=new BookingSnapshotChanged("alice",Guid.NewGuid(),"Accepted snapshot",DateTimeOffset.UtcNow.AddDays(1),2,24690,"MXN","Confirmed",3);
        DashboardRules.Validate(s);
        using var db=new TourLab.Azure.Reservations.Persistence.ReservationsDesignFactory().CreateDbContext([]);
        var row=DashboardSqlTests.Row("snapshot-owner");BookingSnapshots.Add(db,row,TimeProvider.System.GetUtcNow(),Guid.NewGuid());
        var envelope=MessageCodec.Parse(Assert.Single(db.Outbox.Local).EnvelopeJson);
        Assert.Null(envelope.SagaId);Assert.Equal(row.Id,envelope.CorrelationId);
        var document=DashboardRules.Document(s,TimeProvider.System);
        Assert.EndsWith("Z",document.DepartureUtc);Assert.Equal(s.AmountCents,document.AmountCents);Assert.Equal(s.TourNameSnapshot,document.TourNameSnapshot);
        DashboardRules.ValidateDocument(document,s.UserId,s.ReservationId);
        Assert.Throws<ReservationProblem>(()=>DashboardRules.ValidateDocument(document,"forged-owner"));
        Assert.Throws<ReservationProblem>(()=>DashboardRules.ValidateDocument(document with{AmountCents=0},s.UserId));
        Assert.Throws<PoisonMessageException>(()=>DashboardRules.Validate(s with{Participants=7}));
        Assert.Throws<PoisonMessageException>(()=>DashboardRules.Validate(s with{BusinessVersion=0}));
        Assert.Throws<PoisonMessageException>(()=>DashboardRules.Validate(s with{Currency="USD"}));
        Assert.Throws<PoisonMessageException>(()=>DashboardRules.Validate(s with{Status="Invented"}));
        Assert.Throws<PoisonMessageException>(()=>DashboardRules.Validate(s with{DepartureUtc=s.DepartureUtc.ToOffset(TimeSpan.FromHours(1))}));
    }
    [Fact]
    public void RequiredIndexPartitionAndRoutingAreExplicitWithNoDefaultTtl()
    {
        var properties=DashboardRules.Properties();Assert.Equal("/userId",properties.PartitionKeyPath);Assert.Null(properties.DefaultTimeToLive);
        var composite=Assert.Single(properties.IndexingPolicy.CompositeIndexes);Assert.Equal("/departureUtc",composite[0].Path);Assert.Equal("/id",composite[1].Path);
        Assert.Equal(Microsoft.Azure.Cosmos.CompositePathSortOrder.Descending,composite[0].Order);
        Assert.Equal(Microsoft.Azure.Cosmos.CompositePathSortOrder.Ascending,composite[1].Order);
        Assert.Equal([MessageRoutes.Topic],MessageRoutes.Destinations("BookingSnapshotChanged"));
        Assert.Equal(["BookingSnapshotChanged"],BusTopology.Subscriptions["dashboard-events"]);
        Assert.False(BusTopology.Accepts("reservations",true,"BookingSnapshotChanged"));
    }
    [Fact]
    public void CloudCannotUseKeysEmulatorInsecureOrForeignEndpoints()
    {
        Assert.True(DashboardRules.Safe(new(),false));
        Assert.True(DashboardRules.Safe(new(){Enabled=true,Endpoint="https://tourlabazurebookings.documents.azure.com/"},false));
        Assert.False(DashboardRules.Safe(new(){Enabled=true,Endpoint="http://tourlabazurebookings.documents.azure.com/"},false));
        Assert.False(DashboardRules.Safe(new(){Enabled=true,Endpoint="https://foreign.documents.azure.com/"},false));
        Assert.False(DashboardRules.Safe(new(){Enabled=true,Endpoint="https://tourlabazurebookings.documents.azure.com/",Key="secret"},false));
        Assert.False(DashboardRules.Safe(new(){Enabled=true,Local=true,Endpoint="https://localhost:8089/",Key="local",CaPath="ca"},false));
    }
}
