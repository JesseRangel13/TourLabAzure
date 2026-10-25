using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TourLab.Azure.Notifications.Receipts;
namespace TourLab.Azure.Tests;
public sealed class LiveReceiptFactAttribute:FactAttribute
{
    public LiveReceiptFactAttribute(){if(Environment.GetEnvironmentVariable("AZURELAB_RECEIPTS")!="1"||
        Environment.GetEnvironmentVariable("AZURELAB_LIVE_BFF")!="1")Skip="NOT VERIFIED: run Verify-Receipts.ps1 -IncludeHttps with actual Functions host.";}
}
[Collection("SQL Identity")]
public sealed class LiveReceiptTests
{
    [LiveReceiptFact]
    public async Task RoundTripBookingFunctionAttachmentOwnershipAndCancellationPreserveHistoricalReceipt()
    {
        using var alice=LiveBffTests.Client();using var bob=LiveBffTests.Client();using var admin=LiveBffTests.Client();
        await LiveBffTests.Login(alice,"Alice");await LiveBffTests.Login(bob,"Bob");await LiveBffTests.Login(admin,"Admin");
        using var created=await LiveBookingTests.Post(admin,"/api/catalog/v1/admin/tours",new{name="Receipt live "+Guid.NewGuid(),description="Actual Functions trigger evidence"});
        Assert.Equal(HttpStatusCode.Created,created.StatusCode);
        var tour=(await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        using var added=await LiveBookingTests.Post(admin,$"/api/catalog/v1/admin/tours/{tour}/sessions",
            new{startsAtUtc=DateTimeOffset.UtcNow.AddDays(3),unitAmountMinor=12345,capacity=6});
        var session=(await added.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var traceId=Guid.NewGuid().ToString("N");
        var traceparent="00-"+traceId+"-0123456789abcdef-01";
        using var booked=await LiveBookingTests.Post(alice,"/api/reservations/v1/reservations",
            new{sessionId=session,participants=2,expectedUnitAmountMinor=12345,expectedCurrency="MXN"},Guid.NewGuid().ToString(),traceparent);
        Assert.Equal(HttpStatusCode.Accepted,booked.StatusCode);
        var reservation=(await booked.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("reservationId").GetGuid();
        using var budget=new CancellationTokenSource(TimeSpan.FromSeconds(100));
        ReceiptView receipt;
        while(true){
            using var response=await alice.GetAsync("/api/notifications/v1/receipts/reservation/"+reservation,budget.Token);
            if(response.IsSuccessStatusCode){
                var rows=(await response.Content.ReadFromJsonAsync<ReceiptView[]>(budget.Token))!;
                if(rows.Any(x=>x.Status=="Ready")){receipt=Assert.Single(rows);break;}
            }
            await Task.Delay(200,budget.Token);
        }
        Assert.True(receipt.Simulated);
        var path="/api/notifications/v1/receipts/"+receipt.Id+"/content";
        using var download=await alice.GetAsync(path,budget.Token);
        Assert.Equal(HttpStatusCode.OK,download.StatusCode);Assert.Equal("attachment",download.Content.Headers.ContentDisposition!.DispositionType);
        Assert.True(download.Headers.CacheControl!.NoStore);Assert.Equal("text/plain",download.Content.Headers.ContentType!.MediaType);
        var bytes=await download.Content.ReadAsByteArrayAsync(budget.Token);
        Assert.Contains("SIMULATED EDUCATIONAL",System.Text.Encoding.UTF8.GetString(bytes));
        Assert.Contains("24690 MXN cents",System.Text.Encoding.UTF8.GetString(bytes));
        Assert.Equal(HttpStatusCode.NotFound,(await bob.GetAsync(path,budget.Token)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,(await bob.GetAsync("/api/notifications/v1/receipts/reservation/"+reservation,budget.Token)).StatusCode);
        Assert.Equal(HttpStatusCode.OK,(await admin.GetAsync(path,budget.Token)).StatusCode);
        using var cancel=await LiveBookingTests.Post(alice,"/api/reservations/v1/reservations/"+reservation+"/cancel",new{},Guid.NewGuid().ToString());
        Assert.Equal(HttpStatusCode.Accepted,cancel.StatusCode);
        while(true){
            var detail=await alice.GetFromJsonAsync<JsonElement>("/api/reservations/v1/reservations/"+reservation,budget.Token);
            if(detail.GetProperty("status").GetString()=="Cancelled")break;
            await Task.Delay(200,budget.Token);
        }
        Assert.Equal(bytes,await alice.GetByteArrayAsync(path,budget.Token));
        await using var db=BookingSqlTests.Notifications();
        Assert.Equal(1,await db.ReceiptJobs.CountAsync(x=>x.ReservationId==reservation,budget.Token));
        var command=await db.Outbox.SingleAsync(x=>x.EffectKey=="receipt/"+receipt.Id,budget.Token);
        Assert.Contains(traceId,TourLab.Azure.Infrastructure.Messaging.MessageCodec.Parse(command.EnvelopeJson).Traceparent);
        await File.WriteAllTextAsync(Path.Combine(TestSettings.Output,"receipt-live-trace.txt"),traceId,budget.Token);
        var notifications=await db.Notifications.Where(x=>x.ReservationId==reservation).ToListAsync(budget.Token);
        while(notifications.Count<2||notifications.Any(x=>x.Status!="Sent")){
            await Task.Delay(200,budget.Token);db.ChangeTracker.Clear();
            notifications=await db.Notifications.AsNoTracking().Where(x=>x.ReservationId==reservation).ToListAsync(budget.Token);
        }
        Assert.Contains(notifications,x=>x.Kind=="ReservationConfirmed");
        Assert.Contains(notifications,x=>x.Kind=="ReservationCancelled");
    }
}