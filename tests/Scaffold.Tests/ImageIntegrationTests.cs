using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using Azure.Storage.Blobs.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using TourLab.Azure.Catalog.Application;
using TourLab.Azure.Catalog.Images;
using TourLab.Azure.Catalog.Persistence;
using TourLab.Azure.Infrastructure.Security;
using TourLab.Azure.Provisioning;
namespace TourLab.Azure.Tests;

public sealed class ImageFactAttribute:FactAttribute
{
    public ImageFactAttribute(){if(Environment.GetEnvironmentVariable("AZURELAB_IMAGES")!="1"||string.IsNullOrEmpty(Environment.GetEnvironmentVariable("AZURELAB_INTEGRATION_CONFIG")))Skip="NOT VERIFIED: run Verify-Images.ps1 with real SQL and Azurite.";}
}
[Collection("SQL Identity")]
public sealed class ImageIntegrationTests
{
    internal static ImageStorageOptions Settings()=>JsonSerializer.Deserialize<ImageStorageOptions>(JsonDocument.Parse(File.ReadAllText(Path.Combine(TestSettings.Output,"catalog.json"))).RootElement.GetProperty("ImageStorage").GetRawText())!;
    internal static CatalogDb Open()=>new(DatabaseProvisioner.Options<CatalogDb>(TestSettings.Configuration.Runtime["catalog"],"catalog"));
    private sealed class Clock:TimeProvider
    {
        private long ticks=DateTimeOffset.UtcNow.UtcTicks;
        public override DateTimeOffset GetUtcNow()=>new(Interlocked.Read(ref ticks),TimeSpan.Zero);
        public void Advance(TimeSpan duration)=>Interlocked.Add(ref ticks,duration.Ticks);
    }
    private sealed class Boundary(IImageStorage actual):IImageStorage
    {
        public bool PutFails,DeleteFails;
        public Func<Task>? BeforePut;
        public Task PutAsync(string k,Stream s,string t,string h,CancellationToken ct)=>Put(k,s,t,h,ct);
        private async Task Put(string k,Stream s,string t,string h,CancellationToken ct){if(PutFails)throw new IOException("Test-only storage outage");if(BeforePut is not null)await BeforePut();await actual.PutAsync(k,s,t,h,ct);}
        public Task<StoredImage?> HeadAsync(string k,CancellationToken ct)=>actual.HeadAsync(k,ct);
        public Task DeleteAsync(string k,CancellationToken ct)=>DeleteFails?throw new IOException("Test-only delete outage"):actual.DeleteAsync(k,ct);
        public Task<Stream> OpenAsync(string k,CancellationToken ct)=>actual.OpenAsync(k,ct);
    }
    private sealed class Fixture:IDisposable
    {
        public readonly Clock Clock=new();public readonly ImageUploadLimit Limit=new();
        public readonly BlobImageStorage Actual=new(Options.Create(Settings()));
        public readonly Boundary Storage;public Guid Tour;
        public Fixture(){Storage=new(Actual);}
        public async Task Initialize(){await using var db=Open();Tour=(await new CatalogCommands(db,Clock).CreateAsync(new("Images "+Guid.NewGuid(),"Retained AZ-006 evidence"),default)).Id;}
        public async Task<T> Run<T>(Func<TourImages,Task<T>> action){await using var db=Open();return await action(new(db,Storage,Clock,Limit));}
        public Task<TourImageView> Upload(Guid id,byte[]? bytes=null,Func<CancellationToken,Task>? after=null)=>Run(h=>h.UploadAsync(Tour,id,new MemoryStream(bytes??ImageValidationTests.Png),default,after));
        public Task<bool> Recover(Guid id)=>Run(h=>h.ReconcileAsync(default,id));
        public async Task Delete(Guid id){await using var db=Open();await new TourImages(db,Storage,Clock,Limit).DeleteAsync(Tour,id,default);}
        public async Task<TourImage> Row(Guid id){await using var db=Open();return await db.Images.AsNoTracking().SingleAsync(x=>x.Id==id);}
        public void Dispose()=>Limit.Dispose();
    }
    [ImageFact]
    public async Task ActualPrivateBlobUploadDownloadBoundsIdempotencyAndOwnedIds()
    {
        using var f=new Fixture();await f.Initialize();var id=Guid.NewGuid();
        var ready=await f.Upload(id);Assert.Equal("Ready",ready.Status);
        Assert.Equal(ready.Id,(await f.Upload(id)).Id);
        var row=await f.Row(id);Assert.DoesNotContain(id.ToString("N"),row.ObjectKey);
        var container=BlobImageStorage.Create(Settings());
        Assert.Equal(PublicAccessType.None,(await container.GetAccessPolicyAsync()).Value.BlobPublicAccess);
        using var anonymous=new HttpClient();
        Assert.False((await anonymous.GetAsync(container.GetBlobClient(row.ObjectKey).Uri)).IsSuccessStatusCode);
        var content=await f.Run(h=>h.DownloadAsync(f.Tour,id,false,default));
        await using(content.Content){using var memory=new MemoryStream();await content.Content.CopyToAsync(memory);Assert.Equal(ImageValidationTests.Png,memory.ToArray());}
        Assert.Equal(409,(await Assert.ThrowsAsync<CatalogProblem>(()=>f.Upload(id,ImageFixtures.Jpeg))).Status);
        await f.Upload(Guid.NewGuid(),ImageFixtures.Jpeg);await f.Upload(Guid.NewGuid());
        Assert.Equal("ImageCountLimit",(await Assert.ThrowsAsync<CatalogProblem>(()=>f.Upload(Guid.NewGuid()))).Code);
        Assert.Equal(404,(await Assert.ThrowsAsync<CatalogProblem>(()=>f.Run(h=>h.UploadAsync(Guid.NewGuid(),id,new MemoryStream(ImageValidationTests.Png),default)))).Status);
        await using var db=Open();Assert.Equal(3,await db.Images.CountAsync(x=>x.TourId==f.Tour));
    }
    [ImageFact]
    public async Task BlobSuccessBeforeSqlCompletionRecoversWithNewContextAndOwnedHistory()
    {
        using var f=new Fixture();await f.Initialize();var id=Guid.NewGuid();
        await Assert.ThrowsAsync<InvalidOperationException>(()=>f.Upload(id,after:_=>throw new InvalidOperationException("Test-only crash after Blob success")));
        Assert.Equal("Pending",(await f.Row(id)).Status);
        Assert.Empty(await f.Run(h=>h.ListAsync(f.Tour,false,default)));
        f.Clock.Advance(TimeSpan.FromMinutes(3));Assert.True(await f.Recover(id));
        Assert.Equal("Ready",(await f.Row(id)).Status);
        await using var db=Open();Assert.Contains(await db.ImageHistory.Where(x=>x.ImageId==id).ToArrayAsync(),x=>x.Outcome=="Ready");
    }
    [ImageFact]
    public async Task LateUploadAgainstDeleteNeverResurrectsAndTombstoneCleansUncertainLatePut()
    {
        using var f=new Fixture();await f.Initialize();var id=Guid.NewGuid();
        var reached=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        f.Storage.BeforePut=async()=>{reached.SetResult();await release.Task;};
        var uploading=f.Upload(id);await reached.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await f.Delete(id);release.SetResult();
        Assert.Equal("DeletePending",(await uploading).Status);
        Assert.Empty(await f.Run(h=>h.ListAsync(f.Tour,false,default)));
        f.Clock.Advance(TimeSpan.FromMinutes(3));Assert.True(await f.Recover(id));
        var row=await f.Row(id);Assert.Equal("Deleted",row.Status);Assert.Null(await f.Actual.HeadAsync(row.ObjectKey,default));
        await f.Actual.PutAsync(row.ObjectKey,new MemoryStream(ImageValidationTests.Png),row.ContentType,row.Sha256,default);
        f.Clock.Advance(TimeSpan.FromHours(2));Assert.True(await f.Recover(id));Assert.Null(await f.Actual.HeadAsync(row.ObjectKey,default));
        Assert.Equal(409,(await Assert.ThrowsAsync<CatalogProblem>(()=>f.Upload(id))).Status);
    }
    [ImageFact]
    public async Task AbandonedPendingAndFailedDeleteRecoverWithoutPurgingUnrelatedObjects()
    {
        using var f=new Fixture();await f.Initialize();var id=Guid.NewGuid();
        var marker="retained-marker/"+Guid.NewGuid().ToString("N");
        await f.Actual.PutAsync(marker,new MemoryStream(ImageValidationTests.Png),"image/png","marker",default);
        f.Storage.PutFails=true;await Assert.ThrowsAsync<IOException>(()=>f.Upload(id));
        f.Clock.Advance(TimeSpan.FromMinutes(20));Assert.True(await f.Recover(id));
        Assert.Equal("DeletePending",(await f.Row(id)).Status);
        f.Clock.Advance(TimeSpan.FromMinutes(3));f.Storage.DeleteFails=true;
        await Assert.ThrowsAsync<IOException>(()=>f.Recover(id));
        f.Clock.Advance(TimeSpan.FromMinutes(3));f.Storage.DeleteFails=false;Assert.True(await f.Recover(id));
        Assert.Equal("Deleted",(await f.Row(id)).Status);Assert.NotNull(await f.Actual.HeadAsync(marker,default));
        await using var db=Open();Assert.Contains(await db.ImageHistory.Where(x=>x.ImageId==id).ToArrayAsync(),x=>x.Outcome=="RecoveryFailed");
    }
    [ImageFact]
    public async Task TwoRecoveryInstancesClaimOneIntentAndForeignKeyCannotBeUsedAsAnObjectPath()
    {
        using var f=new Fixture();await f.Initialize();var id=Guid.NewGuid();
        await Assert.ThrowsAsync<InvalidOperationException>(()=>f.Upload(id,after:_=>throw new InvalidOperationException()));
        f.Clock.Advance(TimeSpan.FromMinutes(3));
        var results=await Task.WhenAll(f.Recover(id),f.Recover(id));Assert.Single(results,x=>x);
        await using var db=Open();
        await db.Images.Where(x=>x.Id==id).ExecuteUpdateAsync(s=>s.SetProperty(x=>x.ObjectKey,"other-container/path"));
        Assert.Equal("InvalidOwnedImageKey",(await Assert.ThrowsAsync<CatalogProblem>(()=>f.Run(h=>h.DownloadAsync(f.Tour,id,true,default)))).Code);
    }
    [ImageFact]
    public async Task ConflictingBlobCannotBeOverwrittenOrPublishedReady()
    {
        using var f=new Fixture();await f.Initialize();var id=Guid.NewGuid();
        await Assert.ThrowsAsync<InvalidOperationException>(()=>f.Upload(id,after:_=>throw new InvalidOperationException()));
        var row=await f.Row(id);
        await Assert.ThrowsAsync<CatalogProblem>(()=>f.Actual.PutAsync(row.ObjectKey,new MemoryStream(ImageFixtures.Jpeg),"image/jpeg","different",default));
        var container=BlobImageStorage.Create(Settings());
        await container.GetBlobClient(row.ObjectKey).SetMetadataAsync(new Dictionary<string,string>{{"sha256","conflicting-test-metadata"}});
        f.Clock.Advance(TimeSpan.FromMinutes(3));Assert.True(await f.Recover(id));
        Assert.Equal("DeletePending",(await f.Row(id)).Status);
        Assert.Empty(await f.Run(h=>h.ListAsync(f.Tour,false,default)));
        f.Clock.Advance(TimeSpan.FromMinutes(3));Assert.True(await f.Recover(id));
        Assert.Null(await f.Actual.HeadAsync(row.ObjectKey,default));
    }
    [ImageFact]
    public async Task ConcurrentUploadCountAndDownloadIntegrityRemainBounded()
    {
        using var f=new Fixture();await f.Initialize();await f.Upload(Guid.NewGuid());await f.Upload(Guid.NewGuid());
        async Task<bool> Attempt()
        {
            try{await f.Upload(Guid.NewGuid());return true;}
            catch(CatalogProblem error) when(error.Code=="ImageCountLimit"){return false;}
        }
        var results=await Task.WhenAll(Attempt(),Attempt());Assert.Single(results,x=>x);
        await using var db=Open();Assert.Equal(3,await db.Images.CountAsync(x=>x.TourId==f.Tour));
        var row=await db.Images.AsNoTracking().FirstAsync(x=>x.TourId==f.Tour);
        // Actual SDK corruption fixture, no production arbitrary-path endpoint.
        await BlobImageStorage.Create(Settings()).GetBlobClient(row.ObjectKey).UploadAsync(new MemoryStream(ImageFixtures.Jpeg),overwrite:true);
        Assert.Equal("ImageObjectConflict",(await Assert.ThrowsAsync<CatalogProblem>(()=>f.Run(h=>h.DownloadAsync(f.Tour,row.Id,false,default)))).Code);
    }    internal static HttpClient Client(AzureFactory<TourLab.Azure.Catalog.Program> factory,string role)
    {
        var client=factory.CreateClient();using var rsa=RSA.Create();rsa.ImportFromPem(File.ReadAllText(Path.Combine(TestSettings.Output,"human-signing.key")));
        var token=new JwtSecurityToken(BackendSecurity.Issuer,BackendSecurity.Audience("Catalog"),[new Claim("sub","images-"+role),new Claim("role",role)],DateTime.UtcNow.AddSeconds(-1),DateTime.UtcNow.AddMinutes(3),
            new SigningCredentials(new RsaSecurityKey(rsa){CryptoProviderFactory=new(){CacheSignatureProviders=false}},SecurityAlgorithms.RsaSha256));
        client.DefaultRequestHeaders.Authorization=new AuthenticationHeaderValue("Bearer",new JwtSecurityTokenHandler().WriteToken(token));return client;
    }
    [ImageFact]
    public async Task HttpAdminAuthorizationVisibilityDownloadAndInvalidUploadAreEnforced()
    {
        await using var factory=new AzureFactory<TourLab.Azure.Catalog.Program>("catalog");
        using var admin=Client(factory,"Admin");using var tourist=Client(factory,"Tourist");using var anonymous=factory.CreateClient();
        using var created=await admin.PostAsJsonAsync("/v1/admin/tours",new CreateTour("HTTP images "+Guid.NewGuid(),""));
        var tour=(await created.Content.ReadFromJsonAsync<TourView>())!;var id=Guid.NewGuid();
        var path=$"/v1/admin/tours/{tour.Id}/images/{id}";
        Assert.Equal(HttpStatusCode.Forbidden,(await tourist.PutAsync(path,new ByteArrayContent(ImageValidationTests.Png))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,(await anonymous.GetAsync($"/v1/tours/{tour.Id}/images")).StatusCode);
        Assert.Equal(HttpStatusCode.OK,(await admin.PutAsync(path,new ByteArrayContent(ImageValidationTests.Png))).StatusCode);
        using var download=await tourist.GetAsync($"/v1/tours/{tour.Id}/images/{id}/content");Assert.Equal(ImageValidationTests.Png,await download.Content.ReadAsByteArrayAsync());
        Assert.Equal("no-store",download.Headers.CacheControl!.ToString());
        Assert.Equal(HttpStatusCode.NotFound,(await tourist.GetAsync($"/v1/tours/{Guid.NewGuid()}/images/{id}/content")).StatusCode);
        Assert.Equal(HttpStatusCode.UnsupportedMediaType,(await admin.PutAsync($"/v1/admin/tours/{tour.Id}/images/{Guid.NewGuid()}",new ByteArrayContent(new byte[32]))).StatusCode);
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge,(await admin.PutAsync($"/v1/admin/tours/{tour.Id}/images/{Guid.NewGuid()}",new ByteArrayContent(new byte[ValidatedImage.MaximumBytes+1]))).StatusCode);
        await admin.PutAsJsonAsync($"/v1/admin/tours/{tour.Id}",new UpdateTour(tour.Name,"",false,tour.Version));
        Assert.Equal(HttpStatusCode.NotFound,(await tourist.GetAsync($"/v1/tours/{tour.Id}/images/{id}/content")).StatusCode);
        Assert.Equal(HttpStatusCode.Accepted,(await admin.DeleteAsync(path)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,(await admin.GetAsync($"/v1/tours/{tour.Id}/images/{id}/content")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,(await tourist.PostAsJsonAsync(path+"/recover",new{})).StatusCode);
    }
}
