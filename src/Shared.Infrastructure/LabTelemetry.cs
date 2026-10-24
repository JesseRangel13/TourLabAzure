using System.Diagnostics;
using System.Diagnostics.Metrics;
namespace TourLab.Azure.Infrastructure;
public static class LabTelemetry
{
    public static readonly ActivitySource Activities = new("TourLab.Azure");
    private static readonly Meter Meter = new("TourLab.Azure");
    private static readonly Counter<long> Outcomes = Meter.CreateCounter<long>("tourlab.outcomes");
    public static void Count(string operation, string outcome) =>
        Outcomes.Add(1, new KeyValuePair<string, object?>("operation", operation), new KeyValuePair<string, object?>("outcome", outcome));
}