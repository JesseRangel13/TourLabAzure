using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TourLab.Azure.Catalog.Application;
using TourLab.Azure.Catalog.Images;
namespace TourLab.Azure.Tests;

public sealed class LiveImageFactAttribute:FactAttribute
{
    public LiveImageFactAttribute(){if(Environment.GetEnvironmentVariable("AZURELAB_IMAGES")!="1"||Environment.GetEnvironmentVariable("AZURELAB_LIVE_BFF")!="1")Skip="NOT VERIFIED: use Verify-Images.ps1 -IncludeHttps."; }
}
[Collection("SQL Identity")]
public sealed class LiveImageTests
{
    private static async Task<HttpResponseMessage> Send(HttpClient client,HttpMethod method,string path,HttpContent? content=null)
    {
        using var request=new HttpRequestMessage(method,path){Content=content};
        request.Headers.Add("Origin","https://localhost:9443");
        request.Headers.Add("X-AZURELAB-XSRF-TOKEN",await LiveBffTests.Csrf(client));
        return await client.SendAsync(request);
    }
    [LiveImageFact]
    public async Task RoundTripRawUploadThroughHttpsBffEnforcesCsrfRolesAndPrivateDownload()
    {
        using var admin=LiveBffTests.Client();using var alice=LiveBffTests.Client();
        await LiveBffTests.Login(admin,"Admin");await LiveBffTests.Login(alice,"Alice");
        using var created=await Send(admin,HttpMethod.Post,"/api/catalog/v1/admin/tours",JsonContent.Create(new CreateTour("Live Blob "+Guid.NewGuid(),"")));
        var tour=(await created.Content.ReadFromJsonAsync<TourView>())!;var id=Guid.NewGuid();
        var path=$"/api/catalog/v1/admin/tours/{tour.Id}/images/{id}";
        Assert.Equal(HttpStatusCode.BadRequest,(await admin.PutAsync(path,new ByteArrayContent(ImageValidationTests.Png))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,(await Send(alice,HttpMethod.Put,path,new ByteArrayContent(ImageValidationTests.Png))).StatusCode);
        Assert.Equal(HttpStatusCode.OK,(await Send(admin,HttpMethod.Put,path,new ByteArrayContent(ImageValidationTests.Png))).StatusCode);
        var rows=await alice.GetFromJsonAsync<TourImageView[]>($"/api/catalog/v1/tours/{tour.Id}/images");
        var image=Assert.Single(rows!);
        using var download=await alice.GetAsync(image.Url);Assert.Equal(ImageValidationTests.Png,await download.Content.ReadAsByteArrayAsync());
        using(var anonymous=LiveBffTests.Client())Assert.Equal(HttpStatusCode.Unauthorized,(await anonymous.GetAsync(image.Url)).StatusCode);
        Assert.Equal(HttpStatusCode.Accepted,(await Send(admin,HttpMethod.Delete,path)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,(await alice.GetAsync(image.Url)).StatusCode);
    }
    private sealed record RestartFixture(Guid Tour,Guid Ready,Guid Deleted,string DeletedKey);
    [LiveImageFact]
    public async Task StageCrashAfterBlobBeforeSqlWhileCatalogProcessIsStopped()
    {
        await using var db=ImageIntegrationTests.Open();
        var tour=await new CatalogCommands(db,TimeProvider.System).CreateAsync(new("Restart Blob "+Guid.NewGuid(),""),default);
        var storage=new BlobImageStorage(Options.Create(ImageIntegrationTests.Settings()));using var limit=new ImageUploadLimit();
        var handler=new TourImages(db,storage,TimeProvider.System,limit);var ready=Guid.NewGuid();var deleted=Guid.NewGuid();
        async Task Crash(Guid id)
        {
            db.ChangeTracker.Clear();
            await Assert.ThrowsAsync<InvalidOperationException>(()=>handler.UploadAsync(tour.Id,id,new MemoryStream(ImageValidationTests.Png),default,_=>throw new InvalidOperationException("Test-only process crash boundary")));
        }
        await Crash(ready);await Crash(deleted);db.ChangeTracker.Clear();
        await handler.DeleteAsync(tour.Id,deleted,default);
        // Fixture advances only these retained intents' lease deadline to simulate time elapsed while stopped.
        await db.Images.Where(x=>x.Id==ready||x.Id==deleted).ExecuteUpdateAsync(s=>s.SetProperty(x=>x.LeaseUntilUtc,DateTimeOffset.UtcNow.AddMinutes(-1)));
        var key=await db.Images.Where(x=>x.Id==deleted).Select(x=>x.ObjectKey).SingleAsync();
        await File.WriteAllTextAsync(Path.Combine(TestSettings.Output,"image-restart.json"),System.Text.Json.JsonSerializer.Serialize(new RestartFixture(tour.Id,ready,deleted,key)));
    }
    [LiveImageFact]
    public async Task RestartedCatalogWorkerRecoversPendingAndDeletesTombstoneWithoutManualRecovery()
    {
        var fixture=System.Text.Json.JsonSerializer.Deserialize<RestartFixture>(await File.ReadAllTextAsync(Path.Combine(TestSettings.Output,"image-restart.json")))!;
        using var budget=new CancellationTokenSource(TimeSpan.FromSeconds(35));
        while(true)
        {
            await using var db=ImageIntegrationTests.Open();
            var rows=await db.Images.AsNoTracking().Where(x=>x.Id==fixture.Ready||x.Id==fixture.Deleted).ToArrayAsync(budget.Token);
            if(rows.Single(x=>x.Id==fixture.Ready).Status=="Ready"&&rows.Single(x=>x.Id==fixture.Deleted).Status=="Deleted")break;
            await Task.Delay(200,budget.Token);
        }
        var storage=new BlobImageStorage(Options.Create(ImageIntegrationTests.Settings()));
        Assert.Null(await storage.HeadAsync(fixture.DeletedKey,budget.Token));
        using var admin=LiveBffTests.Client();await LiveBffTests.Login(admin,"Admin");
        using var downloaded=await admin.GetAsync($"/api/catalog/v1/tours/{fixture.Tour}/images/{fixture.Ready}/content",budget.Token);
        Assert.Equal(ImageValidationTests.Png,await downloaded.Content.ReadAsByteArrayAsync(budget.Token));
    }
}
