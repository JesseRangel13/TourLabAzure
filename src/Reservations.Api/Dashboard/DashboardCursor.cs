using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TourLab.Azure.Infrastructure.Messaging;
using TourLab.Azure.Reservations.Application;
using TourLab.Azure.Reservations.Persistence;

namespace TourLab.Azure.Reservations.Dashboard;

public sealed record CursorState(string User,string Query,int Size,long Expires,string? Token=null,long After=0,long Upper=0,Guid Run=default);
public sealed class DashboardCursor : IDisposable
{
    private readonly Lazy<IDataProtector> protector;
    private readonly TimeProvider clock;
    private X509Certificate2? certificate;
    private IDisposable? ownedProvider;
    public DashboardCursor(IDataProtectionProvider provider,TimeProvider clock)
    {
        this.clock=clock;protector=new(()=>provider.CreateProtector("TourLab.Azure.Dashboard.Cursor.v1"));
    }
    public DashboardCursor(IOptions<DashboardOptions> options,TimeProvider clock,IHostEnvironment environment)
    {
        this.clock=clock;
        protector=new(()=>{
            var o=options.Value;
            if(!o.Enabled)throw new ReservationProblem(503,"DashboardDisabled");
            var directory=Path.GetFullPath(o.KeyDirectory);
            // Local private key material must remain inside this companion's ignored .local area.
            var root=new DirectoryInfo(environment.ContentRootPath);
            while(root is not null&&!File.Exists(Path.Combine(root.FullName,"TourLab.Azure.slnx")))root=root.Parent;
            if(o.Local&&(root is null||!directory.StartsWith(Path.Combine(root.FullName,".local")+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException("Owned dashboard key directory required.");
            certificate=X509CertificateLoader.LoadPkcs12FromFile(o.CertificatePath,o.CertificatePassword,X509KeyStorageFlags.EphemeralKeySet);
            var provider=DataProtectionProvider.Create(new DirectoryInfo(directory),builder=>builder
                .SetApplicationName("TourLab.Azure.Reservations.Dashboard")
                .ProtectKeysWithCertificate(certificate));
            ownedProvider=provider as IDisposable;
            return provider.CreateProtector("TourLab.Azure.Dashboard.Cursor.v1");
        });
    }
    public string Protect(CursorState state)
    {
        if(state.Token?.Length>2048)throw new ReservationProblem(503,"DashboardCursorTooLarge");
        var value=protector.Value.Protect(JsonSerializer.Serialize(state));
        if(value.Length>4096)throw new ReservationProblem(503,"DashboardCursorTooLarge");
        return value;
    }
    public CursorState Read(string value,string user,string query,int size)
    {
        if(value.Length>4096)throw new ReservationProblem(400,"InvalidDashboardCursor");
        try
        {
            var state=JsonSerializer.Deserialize<CursorState>(protector.Value.Unprotect(value));
            if(state is null||state.User!=user||state.Query!=query||state.Size!=size||state.Expires<=clock.GetUtcNow().ToUnixTimeSeconds()||
                state.Token?.Length>2048||state.After<0||state.Upper<state.After)throw new CryptographicException();
            return state;
        }
        catch(Exception error) when(error is CryptographicException or JsonException or FormatException)
        {throw new ReservationProblem(400,"InvalidDashboardCursor");}
    }
    public void Dispose(){ownedProvider?.Dispose();certificate?.Dispose();}
    public CursorState New(string user,string query,int size)=>new(user,query,size,clock.GetUtcNow().AddMinutes(15).ToUnixTimeSeconds());
}
public sealed record RebuildRequest(int BatchSize=25,string? Cursor=null);
public sealed record RebuildResult(int Enqueued,string? Cursor,Guid Run,bool EventuallyConsistent=true);

public sealed class BookingRebuilder(ReservationsDb db,DashboardCursor cursors,TimeProvider clock)
{
    public async Task<RebuildResult> RebuildAsync(string actor,RebuildRequest input,CancellationToken ct)
    {
        if(input.BatchSize is <1 or >50)throw new ReservationProblem(400,"InvalidRebuildBatch");
        var state=input.Cursor is null?cursors.New(actor,"rebuild-v1",input.BatchSize) with {
            Run=Guid.NewGuid(),Upper=await db.Reservations.MaxAsync(x=>(long?)x.ProjectionSequence,ct)??0
        }:cursors.Read(input.Cursor,actor,"rebuild-v1",input.BatchSize);
        await using var tx=await db.Database.BeginTransactionAsync(ct);
        await SqlOperationLocks.AcquireAsync(db,"reservations",$"dashboard-rebuild/{state.Run}/{state.After}",ct);
        var rows=await db.Reservations.AsNoTracking().Where(x=>x.ProjectionSequence>state.After&&x.ProjectionSequence<=state.Upper)
            .OrderBy(x=>x.ProjectionSequence).Take(input.BatchSize).ToArrayAsync(ct);
        var count=0;
        foreach(var row in rows)
        {
            // Stable run/position/version guards repeat cursor requests without changing business operation IDs.
            var key=$"booking-rebuild/{state.Run}/{row.ProjectionSequence}/{row.Version}";
            if(!await db.Outbox.AnyAsync(x=>x.EffectKey==key,ct))
            {BookingSnapshots.Add(db,row,clock.GetUtcNow(),state.Run,key);count++;}
        }
        db.Set<OperationsAudit>().Add(new(){Id=Guid.NewGuid(),Actor=actor,Action="DashboardRebuildBatch",OccurredAtUtc=clock.GetUtcNow().UtcDateTime});
        await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);
        var after=rows.Length==0?state.Upper:rows[^1].ProjectionSequence;
        var next=after<state.Upper?cursors.Protect(state with {After=after}):null;
        return new(count,next,state.Run);
    }
}
