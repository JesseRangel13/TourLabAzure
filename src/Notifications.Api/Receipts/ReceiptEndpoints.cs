using System.Security.Claims;
using Azure;
using Microsoft.EntityFrameworkCore;
using TourLab.Azure.Infrastructure.Security;
using TourLab.Azure.Notifications.Persistence;
namespace TourLab.Azure.Notifications.Receipts;

public sealed record ReceiptView(Guid Id, Guid ReservationId, long SourceBusinessVersion, string Status,
    DateTimeOffset ConfirmedAtUtc, DateTimeOffset? ReadyAtUtc, string? ErrorCode, bool Simulated = true);
public static class ReceiptEndpoints
{
    public static void MapReceipts(this WebApplication app)
    {
        var api = app.MapGroup("/v1/receipts").RequireAuthorization(BackendSecurity.Human);
        api.MapGet("/reservation/{reservationId:guid}", async (Guid reservationId, HttpContext http, NotificationsDb db, CancellationToken ct) => {
            http.Response.Headers.CacheControl = "no-store";
            var user = http.User.FindFirstValue("sub")!;
            var rows = await db.ReceiptJobs.AsNoTracking().Where(x => x.ReservationId == reservationId &&
                (x.UserId == user || http.User.IsInRole("Admin"))).OrderByDescending(x => x.SourceBusinessVersion).Take(10)
                .Select(x => new ReceiptView(x.Id, x.ReservationId, x.SourceBusinessVersion, x.Status,
                    x.ConfirmedAtUtc, x.ReadyAtUtc, x.ErrorCode, true)).ToArrayAsync(ct);
            return rows.Length == 0 ? Results.NotFound() : Results.Ok(rows);
        });
        api.MapGet("/{id:guid}/content", async (Guid id, HttpContext http, NotificationsDb db, IReceiptStorage storage, CancellationToken ct) => {
            http.Response.Headers.CacheControl = "no-store"; http.Response.Headers.XContentTypeOptions = "nosniff";
            var user = http.User.FindFirstValue("sub")!;
            var job = await db.ReceiptJobs.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id &&
                (x.UserId == user || http.User.IsInRole("Admin")), ct);
            if (job is null) return Results.NotFound();
            if (job.Status != "Ready") return Results.Conflict(new { code = "ReceiptNotReady" });
            try
            {
                using var budget = CancellationTokenSource.CreateLinkedTokenSource(ct); budget.CancelAfter(TimeSpan.FromSeconds(15));
                var bytes = await storage.ReadAsync(job.BlobKey, job.ContentHash, budget.Token);
                return Results.File(bytes, "text/plain; charset=utf-8", $"simulated-confirmation-{job.ReservationId:D}.txt");
            }
            catch (Exception error) when (error is RequestFailedException or ReceiptObjectConflict or InvalidOperationException ||
                (error is OperationCanceledException && !ct.IsCancellationRequested))
            {
                return Results.Problem(statusCode: 503, title: "Confirmation document unavailable",
                    extensions: new Dictionary<string,object?> { ["code"] = "ReceiptUnavailable" });
            }
        });
    }
}