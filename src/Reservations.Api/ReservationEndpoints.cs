using System.Security.Claims;
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
    }
}