using System.Collections.Concurrent;
namespace TourLab.Azure.Infrastructure.Operations;
public sealed class WorkerStatus
{
    private readonly ConcurrentDictionary<string, string> states = new();
    public void Update(string worker, bool enabled, string state, string? error = null) =>
        states[worker] = enabled ? state : "Disabled";
}