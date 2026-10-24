using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using TourLab.Azure.Payments.Persistence;
using TourLab.Azure.Infrastructure.Security;
namespace TourLab.Azure.Payments;
public sealed record PaymentView(Guid Id, Guid ReservationId, string UserId, long AmountMinor, string Currency,
    string Status, string Mode, string? ProviderReference, DateTimeOffset? NextReconcileAtUtc, int ReconcileAttempts, string? Reason, bool Simulated = true);
public static class PaymentEndpoints
{
    private static PaymentView View(PaymentOperation row) => new(row.Id,row.ReservationId,row.UserId,row.AmountMinor,row.Currency,row.Status,row.Mode,row.ProviderReference,row.NextReconcileAtUtc,row.ReconcileAttempts,row.Reason);
    public static void MapPayments(this WebApplication app)
    {
        var api=app.MapGroup("/v1").RequireAuthorization(BackendSecurity.Human);
        api.MapGet("/payments",async (HttpContext http,PaymentsDb db,Guid? reservationId,int? page,int? pageSize,CancellationToken ct)=>{
            var p=page??1;var size=pageSize??10;
            if(p<1||size is <1 or >50||(long)(p-1)*size>int.MaxValue)return Results.BadRequest();
            var caller=http.User.FindFirstValue("sub")!;
            var rows=await db.Operations.AsNoTracking().Where(x=>(http.User.IsInRole("Admin")||x.UserId==caller)&&(!reservationId.HasValue||x.ReservationId==reservationId))
                .OrderBy(x=>x.Id).Skip((p-1)*size).Take(size).ToListAsync(ct);
            return Results.Ok(rows.Select(View));
        });
        api.MapGet("/payments/{id:guid}",async (Guid id,HttpContext http,PaymentsDb db,CancellationToken ct)=>{
            var caller=http.User.FindFirstValue("sub")!;
            var row=await db.Operations.AsNoTracking().SingleOrDefaultAsync(x=>x.Id==id&&(http.User.IsInRole("Admin")||x.UserId==caller),ct);
            return row is null?Results.NotFound():Results.Ok(View(row));
        });
    }
}