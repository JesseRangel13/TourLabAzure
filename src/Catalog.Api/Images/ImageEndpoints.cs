using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;
using Azure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TourLab.Azure.Catalog.Application;
using TourLab.Azure.Infrastructure.Security;

namespace TourLab.Azure.Catalog.Images;

public static class ImageEndpoints
{
    public static void AddTourImages(this IServiceCollection services)
    {
        services.AddOptions<ImageStorageOptions>().Configure<IConfiguration,IHostEnvironment>((o,c,e) => {
            c.GetSection("ImageStorage").Bind(o);
            if (!BlobImageStorage.Safe(o, e.IsDevelopment()))
                throw new InvalidOperationException("Owned private image container and safe storage configuration required.");
        }).ValidateOnStart();
        services.AddRateLimiter(o => { o.RejectionStatusCode=429; o.AddConcurrencyLimiter("image-reads", l => { l.PermitLimit=8; l.QueueLimit=0; }); });
        services.AddSingleton<ImageUploadLimit>(); services.AddSingleton<IImageStorage,BlobImageStorage>();
        services.AddScoped<TourImages>(); services.AddHostedService<ImageRecoveryWorker>();
    }
    public static void MapTourImages(this WebApplication app)
    {
        var api = app.MapGroup("/v1").RequireAuthorization(BackendSecurity.Human).AddEndpointFilter(async (ctx,next) => {
            try { return await next(ctx); }
            catch (CatalogProblem e) { return Problem(ctx.HttpContext,e.Status,e.Code); }
            catch (DbUpdateConcurrencyException) { return Problem(ctx.HttpContext,409,"ImageVersionConflict"); }
            catch (RequestFailedException) { return Problem(ctx.HttpContext,503,"ImageStorageUnavailable"); }
            catch (OperationCanceledException) when (!ctx.HttpContext.RequestAborted.IsCancellationRequested) { return Problem(ctx.HttpContext,503,"ImageStorageTimeout"); }
        });
        api.MapGet("/tours/{tour:guid}/images",(Guid tour,HttpContext h,TourImages images,CancellationToken ct) =>
            images.ListAsync(tour,h.User.IsInRole("Admin"),ct));
        api.MapGet("/tours/{tour:guid}/images/{id:guid}/content",async (Guid tour,Guid id,HttpContext h,TourImages images,CancellationToken ct) => {
            var result=await images.DownloadAsync(tour,id,h.User.IsInRole("Admin"),ct);
            h.Response.Headers.CacheControl="no-store"; h.Response.Headers["X-Content-Type-Options"]="nosniff";
            return Results.Stream(result.Content,result.Type,enableRangeProcessing:false);
        }).RequireRateLimiting("image-reads");
        var admin=api.MapGroup("/admin").RequireAuthorization(BackendSecurity.Admin).RequireRateLimiting("operations-admin");
        admin.MapPut("/tours/{tour:guid}/images/{id:guid}",async (Guid tour,Guid id,HttpContext h,TourImages images,CancellationToken ct) => {
            if(h.Request.ContentLength>ValidatedImage.MaximumBytes)return Results.StatusCode(413);
            return Results.Ok(await images.UploadAsync(tour,id,h.Request.Body,ct));
        });
        admin.MapDelete("/tours/{tour:guid}/images/{id:guid}",async (Guid tour,Guid id,TourImages images,CancellationToken ct) => {
            await images.DeleteAsync(tour,id,ct);return Results.Accepted();
        });
        admin.MapPost("/tours/{tour:guid}/images/{id:guid}/recover",async (Guid tour,Guid id,TourImages images,TourLab.Azure.Catalog.Persistence.CatalogDb db,CancellationToken ct) => {
            if(!await db.Images.AnyAsync(x=>x.TourId==tour&&x.Id==id,ct))return Problem(null,404,"ImageNotFound");
            return Results.Ok(new { processed=await images.ReconcileAsync(ct,id) });
        });
        admin.MapGet("/tours/{tour:guid}/images/{id:guid}/history",async (Guid tour,Guid id,TourLab.Azure.Catalog.Persistence.CatalogDb db,CancellationToken ct) =>
            await db.Images.AnyAsync(x=>x.Id==id&&x.TourId==tour,ct)
                ? Results.Ok(await db.ImageHistory.AsNoTracking().Where(x=>x.ImageId==id).OrderByDescending(x=>x.Id).Take(30).ToArrayAsync(ct))
                : Problem(null,404,"ImageNotFound"));
    }
    private static IResult Problem(HttpContext? h,int status,string code)=>Results.Problem(statusCode:status,title:code,
        extensions:new Dictionary<string,object?> {["code"]=code,["traceId"]=h?.TraceIdentifier??System.Diagnostics.Activity.Current?.TraceId.ToString()});
}
public sealed class ImageRecoveryWorker(IServiceScopeFactory scopes,IOptions<ImageStorageOptions> options,ILogger<ImageRecoveryWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stop)
    {
        if(!options.Value.Enabled)return;
        while(!stop.IsCancellationRequested)
        {
            try
            {
                for(var i=0;i<10;i++)
                {
                    await using var scope=scopes.CreateAsyncScope();
                    if(!await scope.ServiceProvider.GetRequiredService<TourImages>().ReconcileAsync(stop))break;
                }
            }
            catch(OperationCanceledException) when(stop.IsCancellationRequested){return;}
            catch { logger.LogWarning("Image recovery failed; owned SQL intent retained for retry"); }
            await Task.Delay(TimeSpan.FromSeconds(15),stop);
        }
    }
}
