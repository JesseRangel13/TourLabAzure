using Microsoft.EntityFrameworkCore;
namespace TourLab.Azure.Reservations.Persistence;
public sealed class OperationsAudit {
 public Guid Id {get;set;} public string Actor {get;set;}=""; public string Action {get;set;}="";
 public Guid? ReplayId {get;set;} public DateTime OccurredAtUtc {get;set;}
}
public sealed class DlqReplay {
 public Guid Id {get;set;} public string Source {get;set;}=""; public long SequenceNumber {get;set;}
 public string BodyHash {get;set;}=""; public Guid? LogicalMessageId {get;set;}
 public DateTime InspectedAtUtc {get;set;} public string Status {get;set;}="Inspected";
 public Guid? LeaseOwner {get;set;} public DateTime? LeaseUntilUtc {get;set;}
}
public sealed class LabFault { public int Id {get;set;}=1; public string Mode {get;set;}="None"; public int Remaining {get;set;} }
internal static class OperationsModel {
 public static void Configure(ModelBuilder model) {
  model.Entity<OperationsAudit>(b=>{b.ToTable("OperationsAudit");b.HasKey(x=>x.Id);b.Property(x=>x.Actor).HasMaxLength(128);b.Property(x=>x.Action).HasMaxLength(100);b.HasIndex(x=>x.OccurredAtUtc);});
  model.Entity<DlqReplay>(b=>{b.ToTable("DlqReplays");b.HasKey(x=>x.Id);b.Property(x=>x.Source).HasMaxLength(16);b.Property(x=>x.BodyHash).HasMaxLength(64);b.Property(x=>x.Status).HasMaxLength(40);});
  model.Entity<LabFault>(b=>{b.ToTable("Faults");b.HasKey(x=>x.Id);b.Property(x=>x.Mode).HasMaxLength(40);b.HasData(new LabFault());});
 } }
