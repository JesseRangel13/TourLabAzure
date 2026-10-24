using System.Net;
using System.Text.Json;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Options;
using TourLab.Azure.Contracts;
using TourLab.Azure.Infrastructure.Messaging;
using TourLab.Azure.Reservations.Dashboard;
namespace TourLab.Azure.Tests;
public sealed class CosmosFactAttribute:FactAttribute
{
    public CosmosFactAttribute(){if(Environment.GetEnvironmentVariable("AZURELAB_COSMOS")!="1")Skip="NOT VERIFIED: use Verify-Dashboard.ps1 with actual Cosmos and real SQL.";}
}
[Collection("SQL Identity")]
public sealed class CosmosIntegrationTests
{
    internal static DashboardOptions Settings()=>JsonSerializer.Deserialize<DashboardOptions>(JsonDocument.Parse(File.ReadAllText(Path.Combine(TestSettings.Output,"reservations.json"))).RootElement.GetProperty("Dashboard").GetRawText())!;
    internal static CosmosBookingProjection Store()=>new(Options.Create(Settings()),TimeProvider.System);
    internal static BookingSnapshotChanged Snapshot(string? user=null,Guid? id=null,long version=1,string status="AwaitingAvailability")=>
        new(user??"cosmos-"+Guid.NewGuid(),id??Guid.NewGuid(),"Immutable tour snapshot",DateTimeOffset.UtcNow.AddDays(3),2,24690,"MXN",status,version);
    [CosmosFact]
    public async Task RealCosmosUsesConditionalEtagsAndConcurrentVersionsNeverRegress()
    {
        using var store=Store();var first=Snapshot();Assert.True(await store.ApplyAsync(first,default));
        var options=Settings();var connection=CosmosBookingProjection.Create(options);
        using var client=connection.Client;using var http=connection.Http;
        var container=client.GetContainer(options.Database,options.Container);
        var read=await container.ReadItemAsync<BookingDocument>(first.ReservationId.ToString("D"),new PartitionKey(first.UserId));
        await store.ApplyAsync(first with{BusinessVersion=2,Status="AwaitingPayment"},default);
        var stale=await Assert.ThrowsAsync<CosmosException>(()=>container.ReplaceItemAsync(read.Resource,read.Resource.Id,new PartitionKey(first.UserId),new ItemRequestOptions{IfMatchEtag=read.ETag}));
        Assert.Equal(HttpStatusCode.PreconditionFailed,stale.StatusCode);
        var tasks=Enumerable.Range(3,6).Reverse().Select(v=>store.ApplyAsync(first with{BusinessVersion=v,Status=v==8?"Confirmed":"AwaitingConfirmation"},default));
        await Task.WhenAll(tasks);
        Assert.Equal(8,(await store.ReadAsync(first.UserId,first.ReservationId,default))!.BusinessVersion);
        Assert.False(await store.ApplyAsync(first,default));
        Assert.False(await store.ApplyAsync(first with{BusinessVersion=8,Status="Confirmed"},default));
        await Assert.ThrowsAsync<PoisonMessageException>(()=>store.ApplyAsync(first with{BusinessVersion=9,AmountCents=999},default));
        await Assert.ThrowsAsync<PoisonMessageException>(()=>store.ApplyAsync(first with{BusinessVersion=8,Status="Failed"},default));
    }
    [CosmosFact]
    public async Task RealPartitionQueryOrdersTiesAndPagesWithoutLeakingOtherUsers()
    {
        using var store=Store();var user="pages-"+Guid.NewGuid();var baseline=Snapshot(user);
        var snapshots=Enumerable.Range(0,7).Select(i=>baseline with{ReservationId=Guid.NewGuid(),DepartureUtc=baseline.DepartureUtc.AddDays(i/2)}).ToArray();
        foreach(var s in snapshots)await store.ApplyAsync(s,default);
        await store.ApplyAsync(baseline with{UserId="other-"+user},default);
        var seen=new List<BookingDocument>();string? token=null;
        using var budget=new CancellationTokenSource(TimeSpan.FromSeconds(30));
        do{var page=await store.QueryAsync(user,2,token,budget.Token);Assert.InRange(page.Items.Length,0,2);seen.AddRange(page.Items);token=page.Token;}while(token is not null);
        Assert.Equal(7,seen.Count);Assert.All(seen,s=>Assert.Equal(user,s.UserId));
        Assert.Equal(snapshots.OrderByDescending(s=>s.DepartureUtc).ThenBy(s=>s.ReservationId.ToString("D"),StringComparer.Ordinal).Select(s=>s.ReservationId),seen.Select(s=>s.ReservationId));
        Assert.Null(await store.ReadAsync("bob-"+user,snapshots[0].ReservationId,default));
        var c=CosmosBookingProjection.Create(Settings());using var client=c.Client;using var http=c.Http;
        var properties=(await client.GetContainer(Settings().Database,"bookings").ReadContainerAsync()).Resource;
        Assert.Equal("/userId",properties.PartitionKeyPath);Assert.Null(properties.DefaultTimeToLive);
        // vNext accepts the declared composite policy; its index enforcement is NOT VERIFIED.
    }
    [CosmosFact]
    public async Task ConcurrentCreateConflictAndOlderRebuildConvergeToNewerLiveSnapshot()
    {
        using var first=Store();using var second=Store();var s=Snapshot();
        await Task.WhenAll(first.ApplyAsync(s,default),second.ApplyAsync(s with{BusinessVersion=3,Status="Confirmed"},default));
        Assert.Equal(3,(await first.ReadAsync(s.UserId,s.ReservationId,default))!.BusinessVersion);
        var gate=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task OldRebuild(){await gate.Task;await second.ApplyAsync(s with{BusinessVersion=2,Status="AwaitingPayment"},default);}
        var rebuild=OldRebuild();await first.ApplyAsync(s with{BusinessVersion=4,Status="CancellationPending"},default);gate.SetResult();await rebuild;
        var current=await first.ReadAsync(s.UserId,s.ReservationId,default);
        Assert.Equal(4,current!.BusinessVersion);Assert.Equal("CancellationPending",current.Status);
    }
}
