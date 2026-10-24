using System.Diagnostics;
using System.Text.Json;
using TourLab.Azure.Infrastructure;
using TourLab.Azure.Infrastructure.Messaging;
namespace TourLab.Azure.Tests;
public sealed class TelemetryTests {
 [Fact]
 public void W3cParentSurvivesRestoredActivityAndMetadataCannotChangeLogicalFingerprint(){
  using var parent=LabTelemetry.Start("test.parent",ActivityKind.Server,null,null);Assert.NotNull(parent);
  parent.TraceStateString="lab=synthetic";
  using var child=LabTelemetry.Start("test.consumer",ActivityKind.Consumer,parent.Id,parent.TraceStateString);Assert.NotNull(child);
  Assert.Equal(parent.TraceId,child.TraceId);Assert.Equal(parent.SpanId,child.ParentSpanId);
  var envelope=BookingSqlTests.Envelope(new TourLab.Azure.Contracts.TourSessionChanged(Guid.NewGuid(),"Synthetic",true,1,100,"MXN"),Guid.NewGuid());
  Assert.Equal(MessageCodec.Fingerprint(envelope),MessageCodec.Fingerprint(envelope with{DeliveryId=Guid.NewGuid(),Traceparent=parent.Id,Tracestate="lab=another"}));
  Assert.Throws<PoisonMessageException>(()=>MessageCodec.Parse(JsonSerializer.Serialize(envelope with{Tracestate=new string('x',513)},MessageCodec.Json)));
 }
}
