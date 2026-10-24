using System.Diagnostics;
using System.Diagnostics.Metrics;
namespace TourLab.Azure.Infrastructure;
public static class LabTelemetry {
 public static readonly ActivitySource Activities=new("TourLab.Azure");
 private static readonly ActivityListener Listener=Listen();
 private static readonly Meter Meter=new("TourLab.Azure");
 private static readonly Counter<long> Outcomes=Meter.CreateCounter<long>("tourlab.outcomes");
 private static readonly Histogram<double> Diagnostics=Meter.CreateHistogram<double>("tourlab.diagnostic.value");
 private static long duplicates;
 public static long IgnoredDuplicates=>Interlocked.Read(ref duplicates);
 private static ActivityListener Listen(){
  var listener=new ActivityListener{ShouldListenTo=s=>s.Name=="TourLab.Azure",
   Sample=(ref ActivityCreationOptions<ActivityContext> _)=>ActivitySamplingResult.AllDataAndRecorded};
  ActivitySource.AddActivityListener(listener);return listener;
 }
 public static Activity? Start(string name,ActivityKind kind,string? parent,string? state){
  _=Listener;
  return ActivityContext.TryParse(parent,state,true,out var context)?Activities.StartActivity(name,kind,context):Activities.StartActivity(name,kind);
 }
 public static void Count(string operation,string outcome){
  if(operation=="inbox"&&outcome=="duplicate")Interlocked.Increment(ref duplicates);
  Outcomes.Add(1,new KeyValuePair<string,object?>("operation",operation),new KeyValuePair<string,object?>("outcome",outcome));
 }
 public static void Measure(string name,double value)=>Diagnostics.Record(value,new KeyValuePair<string,object?>("measure",name));
}
