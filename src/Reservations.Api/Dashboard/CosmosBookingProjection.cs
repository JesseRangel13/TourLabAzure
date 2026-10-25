using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using Azure.Identity;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Options;
using TourLab.Azure.Contracts;
using TourLab.Azure.Infrastructure.Messaging;

namespace TourLab.Azure.Reservations.Dashboard;

public sealed class DashboardOptions
{
    public bool Enabled { get; set; }
    public bool Local { get; set; }
    public string Endpoint { get; set; } = "";
    public string Key { get; set; } = "";
    public string Database { get; set; } = "tourlab-azure-dashboard";
    public string Container { get; set; } = "bookings";
    public string CaPath { get; set; } = "";
    public string KeyBlobUri { get; set; } = "";
    public string ProtectionKeyIdentifier { get; set; } = "";
    public string KeyDirectory { get; set; } = "";
    public string CertificatePath { get; set; } = "";
    public string CertificatePassword { get; set; } = "";
}
public sealed record BookingDocument(string Id, string UserId, Guid ReservationId, string TourNameSnapshot,
    string DepartureUtc, int Participants, long AmountCents, string Currency, string Status,
    long BusinessVersion, DateTimeOffset ProjectedAtUtc);
public sealed record DashboardPage(BookingDocument[] Items, string? Cursor, bool EventuallyConsistent = true);
public interface IBookingProjection
{
    Task<bool> ApplyAsync(BookingSnapshotChanged snapshot, CancellationToken ct);
    Task<BookingDocument?> ReadAsync(string user, Guid id, CancellationToken ct);
    Task<(BookingDocument[] Items,string? Token)> QueryAsync(string user,int size,string? token,CancellationToken ct);
}
public static class DashboardRules
{
    public const string Query = "SELECT TOP @limit * FROM c WHERE c.userId = @user ORDER BY c.departureUtc DESC, c.id ASC";
    public static readonly string[] States = ["AwaitingAvailability","AwaitingPayment","PaymentUncertain","AwaitingConfirmation","Confirmed","Compensating","CancellationPending","Cancelled","Failed","ManualReview"];
    public static void Validate(BookingSnapshotChanged s)
    {
        if(string.IsNullOrWhiteSpace(s.UserId)||s.UserId.Length>128||s.UserId.Any(char.IsControl)||s.ReservationId==Guid.Empty||
            string.IsNullOrWhiteSpace(s.TourNameSnapshot)||s.TourNameSnapshot.Length>200||s.DepartureUtc==default||s.DepartureUtc.Offset!=TimeSpan.Zero||
            s.Participants is <1 or >6||s.AmountCents<=0||s.Currency!="MXN"||!States.Contains(s.Status,StringComparer.Ordinal)||s.BusinessVersion<1)
            throw new PoisonMessageException("InvalidBookingSnapshot");
    }
    public static BookingDocument Document(BookingSnapshotChanged s,TimeProvider clock) => new(s.ReservationId.ToString("D"),s.UserId,s.ReservationId,
        s.TourNameSnapshot,s.DepartureUtc.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'",System.Globalization.CultureInfo.InvariantCulture),
        s.Participants,s.AmountCents,s.Currency,s.Status,s.BusinessVersion,clock.GetUtcNow());
    public static void ValidateDocument(BookingDocument document,string user,Guid? expected=null)
    {
        if(document.UserId!=user||!Guid.TryParseExact(document.Id,"D",out var id)||document.ReservationId!=id||
            expected is not null&&id!=expected||
            !DateTimeOffset.TryParseExact(document.DepartureUtc,"yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'",System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.AssumeUniversal|System.Globalization.DateTimeStyles.AdjustToUniversal,out var departure)||
            document.ProjectedAtUtc==default||document.ProjectedAtUtc.Offset!=TimeSpan.Zero)
            throw new Application.ReservationProblem(503,"InvalidBookingProjection");
        try{Validate(new(user,id,document.TourNameSnapshot,departure,document.Participants,document.AmountCents,document.Currency,document.Status,document.BusinessVersion));}
        catch(PoisonMessageException){throw new Application.ReservationProblem(503,"InvalidBookingProjection");}
    }
    public static ContainerProperties Properties() => new("bookings","/userId") {
        IndexingPolicy = new IndexingPolicy {
            Automatic=true, IndexingMode=IndexingMode.Consistent,
            IncludedPaths={new IncludedPath{Path="/*"}},
            CompositeIndexes={new System.Collections.ObjectModel.Collection<CompositePath>{
                new(){Path="/departureUtc",Order=CompositePathSortOrder.Descending},
                new(){Path="/id",Order=CompositePathSortOrder.Ascending}}}
        }
    };
    public static bool Safe(DashboardOptions o,bool development)
    {
        if(!o.Enabled)return true;
        if(o.Database!="tourlab-azure-dashboard"||o.Container!="bookings"||!Uri.TryCreate(o.Endpoint,UriKind.Absolute,out var uri)||uri.Scheme!="https"||
            !string.IsNullOrEmpty(uri.Query)||!string.IsNullOrEmpty(uri.UserInfo)||uri.AbsolutePath!="/")return false;
        return o.Local ? development&&uri.Host=="localhost"&&uri.Port==8089&&!string.IsNullOrEmpty(o.Key)&&!string.IsNullOrEmpty(o.CaPath)
            : string.IsNullOrEmpty(o.Key)&&string.IsNullOrEmpty(o.CaPath)&&string.IsNullOrEmpty(o.KeyDirectory)&&string.IsNullOrEmpty(o.CertificatePath)&&string.IsNullOrEmpty(o.CertificatePassword)&&uri.IsDefaultPort&&uri.Host.StartsWith("tourlabazure",StringComparison.Ordinal)&&uri.Host.EndsWith(".documents.azure.com",StringComparison.Ordinal);
    }
}
public sealed class CosmosBookingProjection : IBookingProjection, IDisposable
{
    private readonly DashboardOptions options;
    private readonly TimeProvider clock;
    private readonly Lazy<(CosmosClient Client,HttpClient? Http)> connection;
    public CosmosBookingProjection(IOptions<DashboardOptions> value,TimeProvider clock)
    {
        options=value.Value;this.clock=clock;
        connection=new(()=>Create(options));
    }
    public static (CosmosClient Client,HttpClient? Http) Create(DashboardOptions o)
    {
        var settings=new CosmosClientOptions {
            ConnectionMode=ConnectionMode.Gateway,RequestTimeout=TimeSpan.FromSeconds(6),
            MaxRetryAttemptsOnRateLimitedRequests=0,MaxRetryWaitTimeOnRateLimitedRequests=TimeSpan.Zero,
            UseSystemTextJsonSerializerWithOptions=new JsonSerializerOptions(JsonSerializerDefaults.Web),
            CosmosClientTelemetryOptions=new CosmosClientTelemetryOptions{DisableDistributedTracing=true}
        };
        HttpClient? http=null;
        if(o.Local)
        {
            var handler=new SocketsHttpHandler{MaxConnectionsPerServer=8,ConnectTimeout=TimeSpan.FromSeconds(3),AllowAutoRedirect=false};
            handler.SslOptions.CertificateChainPolicy=new X509ChainPolicy{TrustMode=X509ChainTrustMode.CustomRootTrust,RevocationMode=X509RevocationMode.NoCheck};
            handler.SslOptions.CertificateChainPolicy.CustomTrustStore.Add(X509CertificateLoader.LoadCertificateFromFile(o.CaPath));
            http=new HttpClient(handler){Timeout=Timeout.InfiniteTimeSpan};
            settings.HttpClientFactory=()=>http;
        }
        var client=o.Local?new CosmosClient(o.Endpoint,o.Key,settings):
            new CosmosClient(o.Endpoint,new ManagedIdentityCredential(ManagedIdentityId.SystemAssigned),settings);
        return(client,http);
    }
    private Container Container => options.Enabled?connection.Value.Client.GetContainer(options.Database,options.Container):
        throw new Application.ReservationProblem(503,"DashboardDisabled");
    public async Task<bool> ApplyAsync(BookingSnapshotChanged snapshot,CancellationToken ct)
    {
        DashboardRules.Validate(snapshot);
        var next=DashboardRules.Document(snapshot,clock);var key=new PartitionKey(snapshot.UserId);
        using var budget=CancellationTokenSource.CreateLinkedTokenSource(ct);budget.CancelAfter(TimeSpan.FromSeconds(20));
        for(var attempt=0;attempt<5;attempt++)
        {
            ItemResponse<BookingDocument>? current=null;
            try{current=await Container.ReadItemAsync<BookingDocument>(next.Id,key,cancellationToken:budget.Token);}
            catch(CosmosException error) when(error.StatusCode==HttpStatusCode.NotFound){}
            if(current is not null)
            {
                var old=current.Resource;
                DashboardRules.ValidateDocument(old,snapshot.UserId,snapshot.ReservationId);
                if(old.BusinessVersion>next.BusinessVersion)return false;
                if(old.BusinessVersion==next.BusinessVersion)
                {
                    if(old with {ProjectedAtUtc=next.ProjectedAtUtc} != next)throw new PoisonMessageException("BookingVersionConflict");
                    return false;
                }
                if(old.UserId!=next.UserId||old.ReservationId!=next.ReservationId||old.TourNameSnapshot!=next.TourNameSnapshot||
                    old.DepartureUtc!=next.DepartureUtc||old.Participants!=next.Participants||old.AmountCents!=next.AmountCents||old.Currency!=next.Currency)
                    throw new PoisonMessageException("ImmutableBookingSnapshotChanged");
            }
            try
            {
                if(current is null)await Container.CreateItemAsync(next,key,cancellationToken:budget.Token);
                else await Container.ReplaceItemAsync(next,next.Id,key,new ItemRequestOptions{IfMatchEtag=current.ETag},budget.Token);
                return true;
            }
            catch(CosmosException error) when(error.StatusCode is HttpStatusCode.Conflict or HttpStatusCode.PreconditionFailed)
            {await Task.Delay(TimeSpan.FromMilliseconds(10*(attempt+1)),budget.Token);}
        }
        throw new Application.ReservationProblem(503,"DashboardContention");
    }
    public async Task<BookingDocument?> ReadAsync(string user,Guid id,CancellationToken ct)
    {
        try{var document=(await Container.ReadItemAsync<BookingDocument>(id.ToString("D"),new PartitionKey(user),cancellationToken:ct)).Resource;
            DashboardRules.ValidateDocument(document,user,id);return document;}
        catch(CosmosException error) when(error.StatusCode==HttpStatusCode.NotFound){return null;}
    }
    private sealed record Position(string Departure,string Id);
    public async Task<(BookingDocument[] Items,string? Token)> QueryAsync(string user,int size,string? token,CancellationToken ct)
    {
        if(size is <1 or >20)throw new Application.ReservationProblem(400,"InvalidDashboardPage");
        Position? position=null;
        if(token is not null)
        {
            try
            {
                position=JsonSerializer.Deserialize<Position>(token);
                if(position is null||!Guid.TryParseExact(position.Id,"D",out _)||
                    !DateTime.TryParseExact(position.Departure,"yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'",System.Globalization.CultureInfo.InvariantCulture,
                        System.Globalization.DateTimeStyles.AssumeUniversal,out _))throw new JsonException();
            }
            catch(JsonException){throw new Application.ReservationProblem(400,"InvalidDashboardCursor");}
        }
        var query=position is null?DashboardRules.Query:
            "SELECT TOP @limit * FROM c WHERE c.userId = @user AND (c.departureUtc < @departure OR (c.departureUtc = @departure AND c.id > @id)) ORDER BY c.departureUtc DESC, c.id ASC";
        var definition=new QueryDefinition(query).WithParameter("@user",user).WithParameter("@limit",size+1);
        if(position is not null)definition.WithParameter("@departure",position.Departure).WithParameter("@id",position.Id);
        using var iterator=Container.GetItemQueryIterator<BookingDocument>(definition,requestOptions:
            new QueryRequestOptions{PartitionKey=new PartitionKey(user),MaxItemCount=size+1,MaxConcurrency=1,MaxBufferedItemCount=size+1,ResponseContinuationTokenLimitInKb=1});
        var items=new List<BookingDocument>();
        // TOP provides a server-side bound even where an emulator ignores MaxItemCount.
        for(var page=0;page<4&&iterator.HasMoreResults;page++)
        {
            var response=await iterator.ReadNextAsync(ct);
            if(items.Count+response.Count>size+1)throw new Application.ReservationProblem(503,"DashboardQueryBudget");
            foreach(var document in response)DashboardRules.ValidateDocument(document,user);
            items.AddRange(response);
        }
        if(iterator.HasMoreResults)throw new Application.ReservationProblem(503,"DashboardQueryBudget");
        var visible=items.Take(size).ToArray();
        return(visible,items.Count>size?JsonSerializer.Serialize(new Position(visible[^1].DepartureUtc,visible[^1].Id)):null);
    }    public void Dispose(){if(connection.IsValueCreated){connection.Value.Client.Dispose();connection.Value.Http?.Dispose();}}
}
