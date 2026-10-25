using System.Text;
using TourLab.Azure.Contracts;
using TourLab.Azure.Infrastructure.Messaging;
using TourLab.Azure.Notifications.Receipts;
namespace TourLab.Azure.Tests;
public sealed class ReceiptUnitTests
{
    internal static ConfirmationSnapshot Snapshot(long version = 4) => new(version,"Accepted tour",
        new DateTimeOffset(2030,1,1,0,0,0,TimeSpan.Zero),2,24690,"MXN",
        new DateTimeOffset(2026,10,24,0,0,0,TimeSpan.Zero));
    [Fact]
    public void ImmutableBytesAreCultureIndependentAndClearlyHistoricalAndSimulated()
    {
        var id=Guid.NewGuid();var s=Snapshot();var before=ReceiptDocument.Render(id,s);
        var culture=System.Globalization.CultureInfo.CurrentCulture;
        try {
            System.Globalization.CultureInfo.CurrentCulture=new("es-MX");
            Assert.Equal(before,ReceiptDocument.Render(id,s));
        } finally {System.Globalization.CultureInfo.CurrentCulture=culture;}
        var text=Encoding.UTF8.GetString(before);
        Assert.Contains("SIMULATED EDUCATIONAL",text);Assert.Contains("Not a legal tax invoice",text);
        Assert.Contains("24690 MXN cents",text);Assert.Contains("does not describe the current booking status",text);
        Assert.True(before.Length<ReceiptDocument.MaximumBytes);
    }
    [Fact]
    public void ExistingValidTourNamesArePreservedAndEscapedWithoutForgingDocumentLines()
    {
        var name="Accepted\nConfirmed: forged\tname";
        var bytes=ReceiptDocument.Render(Guid.NewGuid(),Snapshot() with{TourName=name});
        var text=Encoding.UTF8.GetString(bytes);
        Assert.Contains(System.Text.Json.JsonSerializer.Serialize(name),text);
        Assert.DoesNotContain("\nConfirmed: forged",text);
    }
    [Fact]
    public void InvalidMoneyVersionTimeOrOversizeNameCannotProduceADocument()
    {
        var valid=Snapshot();
        foreach(var s in new[]{valid with{SourceBusinessVersion=0},valid with{AmountMinor=-1},
            valid with{Currency="USD"},valid with{Participants=7},valid with{TourName=new string('a',201)},
            valid with{ConfirmedAtUtc=valid.ConfirmedAtUtc.ToOffset(TimeSpan.FromHours(1))}})
            Assert.Throws<PoisonMessageException>(()=>ReceiptDocument.Render(Guid.NewGuid(),s));
    }
    [Fact]
    public void ReceiptCommandUsesOnlyItsQueueAndStorageRejectsForeignOrCloudLocalSettings()
    {
        Assert.Equal(["tourlab-azure-receipt-jobs"],MessageRoutes.Destinations(nameof(GenerateBookingReceipt)));
        Assert.False(BlobReceiptStorage.Safe(new(){Enabled=true,Local=true,ConnectionString="UseDevelopmentStorage=true"},true));
        Assert.False(BlobReceiptStorage.Safe(new(){Enabled=true,Container="foreign"},true));
        Assert.False(BlobReceiptStorage.Safe(new(){Enabled=true,ServiceUri="http://tourlabazure.blob.core.windows.net"},false));
    }
}