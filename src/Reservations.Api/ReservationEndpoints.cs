using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using TourLab.Azure.Reservations.Persistence;
using TourLab.Azure.Reservations.Application;
using TourLab.Azure.Infrastructure.Security;
namespace TourLab.Azure.Reservations;
public static class ReservationEndpoints
{
    public static void MapReservations(this WebApplication app)
    {
        var api = app.MapGroup("/v1").RequireAuthorization(BackendSecurity.Human).AddEndpointFilter(async (context, next) =>
        {
            try { return await next(context); }
            catch (ReservationProblem error) { return Results.Problem(statusCode: error.Status, title: error.Code,
                extensions: new Dictionary<string,object?> { ["code"] = error.Code, ["traceId"] = context.HttpContext.TraceIdentifier }); }
        });
        api.MapPost("/reservations", async (CreateReservation command, HttpContext http, ReservationCreator handler, CancellationToken ct) => {
            var result = await handler.CreateAsync(http.User.FindFirstValue("sub")!, http.Request.Headers["Idempotency-Key"].ToString(), command, ct);
            http.Response.Headers.Location = result.Location;
            return Results.Content(result.ResponseBody, "application/json", statusCode: result.ResponseStatus);
        });
        api.MapGet("/reservations", (HttpContext http, ReservationQueries queries, string? userId, string? status, int? page, int? pageSize, CancellationToken ct) =>
            queries.ListAsync(http.User.FindFirstValue("sub")!, http.User.IsInRole("Admin"), userId, status, page??1, pageSize??10, ct));
        api.MapGet("/reservations/{id:guid}", (Guid id, HttpContext http, ReservationQueries queries, CancellationToken ct) =>
            queries.DetailAsync(id, http.User.FindFirstValue("sub")!, http.User.IsInRole("Admin"), ct));
        api.MapGet("/reservations/{id:guid}/timeline", (Guid id, HttpContext http, ReservationQueries queries, CancellationToken ct) =>
            queries.TimelineAsync(id, http.User.FindFirstValue("sub")!, http.User.IsInRole("Admin"), ct));
        api.MapPost("/reservations/{id:guid}/cancel",async(Guid id,HttpContext http,SagaCommands commands,CancellationToken ct)=>{
            if(http.Request.Headers["Idempotency-Key"].Count!=1)throw new ReservationProblem(400,"IdempotencyKeyRequired");
            var result=await commands.CancelAsync(id,http.User.FindFirstValue("sub")!,http.User.IsInRole("Admin"),http.Request.Headers["Idempotency-Key"].ToString(),ct);
            http.Response.Headers.Location=result.Location;return Results.Content(result.ResponseBody,"application/json",statusCode:result.ResponseStatus);
        });
        api.MapGet("/projections/tour-sessions",async(ReservationsDb db,int? page,int? pageSize,CancellationToken ct)=>{
            var p=page??1;var size=pageSize??10;if(p<1||size is <1 or >50||(long)(p-1)*size>int.MaxValue)throw new ReservationProblem(400,"InvalidPagination");
            return await db.Projections.AsNoTracking().OrderBy(x=>x.SessionId).Skip((p-1)*size).Take(size).ToArrayAsync(ct);
        });
        var admin=api.MapGroup("/admin/sagas").RequireAuthorization(BackendSecurity.Admin).RequireRateLimiting("operations-admin");
        admin.MapGet("",async(ReservationsDb db,string? state,int? page,CancellationToken ct)=>{
            var p=page??1;if(p<1||p>1000000)throw new ReservationProblem(400,"InvalidPagination");
            return (await db.Sagas.AsNoTracking().Where(x=>state==null||x.State==state).OrderBy(x=>x.Id).Skip((p-1)*20).Take(20).ToArrayAsync(ct)).Select(SagaCommands.View);
        });
        admin.MapPost("/{id:guid}/retry-compensation",async(Guid id,HttpContext http,SagaCommands commands,CancellationToken ct)=>
            Results.Json(await commands.RetryAsync(id,http.User.FindFirstValue("sub")!,ct),statusCode:202));
    }
}