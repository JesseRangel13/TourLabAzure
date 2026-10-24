using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using TourLab.Azure.Notifications.Persistence;
using TourLab.Azure.Infrastructure.Security;
namespace TourLab.Azure.Notifications;
public sealed record NotificationView(Guid Id,Guid ReservationId,string UserId,string Kind,string Subject,string Body,
    string Status,DateTimeOffset CreatedAtUtc,DateTimeOffset? SentAtUtc,bool Simulated=true);
public sealed record NotificationPage(NotificationView[] Items,int Total,int Page,int PageSize);
public static class NotificationEndpoints
{
    private static NotificationView View(Notification n)=>new(n.Id,n.ReservationId,n.UserId,n.Kind,n.Subject,n.Body,n.Status,n.CreatedAtUtc,n.SentAtUtc);
    public static void MapNotifications(this WebApplication app)
    {
        var api=app.MapGroup("/v1").RequireAuthorization(BackendSecurity.Human);
        api.MapGet("/notifications",async(HttpContext http,NotificationsDb db,Guid? reservationId,int? page,int? pageSize,CancellationToken ct)=>{
            var p=page??1;var size=pageSize??10;if(p<1||size is <1 or >50||(long)(p-1)*size>int.MaxValue)return Results.BadRequest();
            var caller=http.User.FindFirstValue("sub")!;
            var query=db.Notifications.AsNoTracking().Where(x=>(http.User.IsInRole("Admin")||x.UserId==caller)&&(!reservationId.HasValue||x.ReservationId==reservationId));
            var total=await query.CountAsync(ct);
            var rows=await query.OrderByDescending(x=>x.CreatedAtUtc).ThenBy(x=>x.Id).Skip((p-1)*size).Take(size).ToListAsync(ct);
            return Results.Ok(new NotificationPage(rows.Select(View).ToArray(),total,p,size));
        });
        api.MapGet("/notifications/{id:guid}",async(Guid id,HttpContext http,NotificationsDb db,CancellationToken ct)=>{
            var caller=http.User.FindFirstValue("sub")!;
            var row=await db.Notifications.AsNoTracking().SingleOrDefaultAsync(x=>x.Id==id&&(http.User.IsInRole("Admin")||x.UserId==caller),ct);
            return row is null?Results.NotFound():Results.Ok(View(row));
        });
    }
}