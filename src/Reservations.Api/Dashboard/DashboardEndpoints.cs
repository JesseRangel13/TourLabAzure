using System.Security.Claims;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Options;
using TourLab.Azure.Infrastructure.Security;
using TourLab.Azure.Reservations.Application;
namespace TourLab.Azure.Reservations.Dashboard;

public static class DashboardEndpoints
{
    public static void AddDashboard(this IServiceCollection services)
    {
        services.AddOptions<DashboardOptions>().Configure<IConfiguration,IHostEnvironment>((o,c,e)=>{
            c.GetSection("Dashboard").Bind(o);
            if(!DashboardRules.Safe(o,e.IsDevelopment()))throw new InvalidOperationException("Safe owned Cosmos configuration is required.");
        }).ValidateOnStart();
        services.AddSingleton<IBookingProjection,CosmosBookingProjection>();
        services.AddSingleton(sp=>new DashboardCursor(sp.GetRequiredService<IOptions<DashboardOptions>>(),sp.GetRequiredService<TimeProvider>(),sp.GetRequiredService<IHostEnvironment>()));
        services.AddScoped<DashboardConsumer>();services.AddScoped<BookingRebuilder>();services.AddHostedService<DashboardWorker>();
    }
    public static void MapDashboard(this WebApplication app)
    {
        var api=app.MapGroup("/v1").RequireAuthorization(BackendSecurity.Human).AddEndpointFilter(async(ctx,next)=>{
            try{return await next(ctx);}
            catch(ReservationProblem error){return Problem(ctx.HttpContext,error.Status,error.Code);}
            catch(CosmosException){return Problem(ctx.HttpContext,503,"DashboardUnavailable");}
            catch(HttpRequestException){return Problem(ctx.HttpContext,503,"DashboardUnavailable");}
            catch(OperationCanceledException) when(!ctx.HttpContext.RequestAborted.IsCancellationRequested){return Problem(ctx.HttpContext,503,"DashboardUnavailable");}
        });
        api.MapGet("/dashboard/bookings",async(HttpContext h,int? pageSize,string? cursor,IBookingProjection store,DashboardCursor cursors,CancellationToken ct)=>{
            var user=h.User.FindFirstValue("sub")!;var size=pageSize??10;
            if(size is <1 or >20)throw new ReservationProblem(400,"InvalidDashboardPage");
            var state=cursor is null?cursors.New(user,"departure-desc-id-asc-v1",size):cursors.Read(cursor,user,"departure-desc-id-asc-v1",size);
            using var budget=CancellationTokenSource.CreateLinkedTokenSource(ct);budget.CancelAfter(TimeSpan.FromSeconds(15));
            var result=await store.QueryAsync(user,size,state.Token,budget.Token);
            h.Response.Headers.CacheControl="no-store";
            return new DashboardPage(result.Items,result.Token is null?null:cursors.Protect(state with {Token=result.Token}));
        });
        api.MapGet("/dashboard/bookings/{id:guid}",async(Guid id,HttpContext h,IBookingProjection store,CancellationToken ct)=>{
            using var budget=CancellationTokenSource.CreateLinkedTokenSource(ct);budget.CancelAfter(TimeSpan.FromSeconds(15));
            var row=await store.ReadAsync(h.User.FindFirstValue("sub")!,id,budget.Token);
            h.Response.Headers.CacheControl="no-store";
            return row is null?Problem(h,404,"BookingProjectionNotFound"):Results.Ok(row);
        });
        api.MapPost("/admin/dashboard/rebuild",async(HttpContext h,RebuildRequest input,BookingRebuilder rebuilder,IOptions<DashboardOptions> options,CancellationToken ct)=>{
            if(!options.Value.Enabled)throw new ReservationProblem(503,"DashboardDisabled");
            return Results.Json(await rebuilder.RebuildAsync(h.User.FindFirstValue("sub")!,input,ct),statusCode:202);
        }).RequireAuthorization(BackendSecurity.Admin).RequireRateLimiting("operations-admin");
    }
    private static IResult Problem(HttpContext h,int status,string code)=>Results.Problem(statusCode:status,title:code,
        extensions:new Dictionary<string,object?>{["code"]=code,["traceId"]=h.TraceIdentifier});
}
