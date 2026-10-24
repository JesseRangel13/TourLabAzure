using System.Collections.Concurrent;
namespace TourLab.Azure.Infrastructure.Operations;
public sealed record WorkerDiagnostic(string Name,bool Enabled,string Status,DateTimeOffset? LastSuccessUtc,string? LastFailureType);
public sealed class WorkerStatus {
 private readonly ConcurrentDictionary<string,WorkerDiagnostic> states=new();
 public void Update(string name,bool enabled,string status,string? error=null)=>states.AddOrUpdate(name,
  _=>new(name,enabled,status,status=="Running"?DateTimeOffset.UtcNow:null,error),
  (_,old)=>old with{Enabled=enabled,Status=status,LastSuccessUtc=status=="Running"?DateTimeOffset.UtcNow:old.LastSuccessUtc,LastFailureType=error??old.LastFailureType});
 public WorkerDiagnostic[] Snapshot()=>states.Values.OrderBy(x=>x.Name).ToArray();
}
