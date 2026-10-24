using Microsoft.EntityFrameworkCore;
using TourLab.Azure.Catalog.Application;
using TourLab.Azure.Infrastructure.Security;

namespace TourLab.Azure.Catalog;

public static class CatalogEndpoints
{
    public static void MapCatalog(this WebApplication app)
    {
        var api = app.MapGroup("/v1").AddEndpointFilter(async (context, next) =>
        {
            try { return await next(context); }
            catch (CatalogProblem error) { return Problem(context.HttpContext, error.Status, error.Code); }
            catch (DbUpdateConcurrencyException) { return Problem(context.HttpContext, 409, "VersionConflict"); }
        });
        var human = api.MapGroup("").RequireAuthorization(BackendSecurity.Human);
        human.MapGet("/tours", (string? search, int? page, int? pageSize, HttpContext http, CatalogQueries queries, CancellationToken ct) =>
            queries.SearchAsync(search, page ?? 1, pageSize ?? 10, http.User.IsInRole("Admin"), ct));
        human.MapGet("/tours/{id:guid}", (Guid id, HttpContext http, CatalogQueries queries, CancellationToken ct) =>
            queries.DetailAsync(id, http.User.IsInRole("Admin"), ct));
        human.MapGet("/tours/{id:guid}/sessions", (Guid id, HttpContext http, CatalogQueries queries, CancellationToken ct) =>
            queries.SessionsAsync(id, http.User.IsInRole("Admin"), ct));
        var admin = human.MapGroup("/admin").RequireAuthorization(BackendSecurity.Admin);
        admin.MapPost("/tours", async (CreateTour command, CatalogCommands handler, CancellationToken ct) =>
        {
            var tour = await handler.CreateAsync(command, ct);
            return Results.Created($"/api/catalog/v1/tours/{tour.Id}", tour);
        }).Produces<TourView>(StatusCodes.Status201Created);
        admin.MapPut("/tours/{id:guid}", (Guid id, UpdateTour command, CatalogCommands handler, CancellationToken ct) =>
            handler.UpdateAsync(id, command, ct));
        admin.MapPost("/tours/{id:guid}/sessions", async (Guid id, CreateSession command, CatalogCommands handler, CancellationToken ct) =>
        {
            var session = await handler.CreateSessionAsync(id, command, ct);
            return Results.Created($"/api/catalog/v1/tours/{id}/sessions", session);
        }).Produces<SessionView>(StatusCodes.Status201Created);
        admin.MapPut("/sessions/{id:guid}/price", (Guid id, UpdatePrice command, CatalogCommands handler, CancellationToken ct) =>
            handler.PriceAsync(id, command, ct));
        admin.MapPut("/sessions/{id:guid}/capacity", (Guid id, UpdateCapacity command, CatalogCommands handler, CancellationToken ct) =>
            handler.CapacityAsync(id, command, ct));
        api.MapPost("/internal/quotes", (CreateQuote command, QuoteHandler handler, CancellationToken ct) =>
            handler.HandleAsync(command, ct)).RequireAuthorization(BackendSecurity.Quote);
    }

    private static IResult Problem(HttpContext http, int status, string code) =>
        Results.Problem(statusCode: status, title: code,
            extensions: new Dictionary<string, object?> { ["code"] = code, ["traceId"] = http.TraceIdentifier });
}