using Microsoft.EntityFrameworkCore;
using TourLab.Azure.Infrastructure.Messaging;
using TourLab.Azure.Infrastructure.Operations;
using TourLab.Azure.Infrastructure.Security;
using TourLab.Azure.Payments.Persistence;
namespace TourLab.Azure.Payments;
public sealed record ProviderResolution(string Outcome);
public static class PaymentAdmin {
 public static void MapPaymentAdmin(this WebApplication app){
  var admin=app.MapGroup("/v1/admin").RequireAuthorization(BackendSecurity.Admin).RequireRateLimiting("operations-admin").AddEndpointFilter(async(context,next)=>{
   try{return await next(context);}catch(OperationsProblem error){return Results.Problem(statusCode:error.Status,title:error.Code,extensions:new Dictionary<string,object?>{["code"]=error.Code});}
  });
  admin.MapPost("/fake-provider/{id:guid}/resolve",async(Guid id,ProviderResolution input,HttpContext http,PaymentsDb db,ServiceOperations operations,TimeProvider clock,CancellationToken ct)=>{
   operations.RequireLab();if(input.Outcome is not ("Succeeded" or "NotCharged"))throw new OperationsProblem(400,"InvalidProviderResolution");
   await using var tx=await db.Database.BeginTransactionAsync(ct);
   await SqlOperationLocks.AcquireAsync(db,"payments","provider/"+id,ct);
   var effect=await db.Effects.SingleOrDefaultAsync(x=>x.Id==id,ct)??throw new OperationsProblem(404,"ProviderEffectNotFound");
   if(effect.Mode!="UnknownUntilAdminResolution"||(effect.Status!="Unknown"&&effect.Status!=input.Outcome))throw new OperationsProblem(409,"ProviderResolutionConflict");
   effect.Status=input.Outcome;effect.Reference=input.Outcome=="Succeeded"?"SIM-"+id.ToString("N"):null;
   db.Audit.Add(new(){Id=Guid.NewGuid(),Actor=OperationsRegistration.Actor(http),Action="Simulated provider resolution "+id+": "+input.Outcome,OccurredAtUtc=clock.GetUtcNow()});
   await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);return Results.Json(new{input.Outcome,simulated=true},statusCode:202);
  });
  admin.MapPost("/payments/{id:guid}/reconcile",async(Guid id,HttpContext http,PaymentsDb db,TimeProvider clock,CancellationToken ct)=>{
   await using var tx=await db.Database.BeginTransactionAsync(ct);
   var row=(await db.Operations.FromSqlInterpolated($"SELECT * FROM [payments].[Operations] WITH(UPDLOCK,HOLDLOCK) WHERE Id={id}").ToListAsync(ct)).SingleOrDefault()??throw new OperationsProblem(404,"PaymentNotFound");
   if(row.Status!="Unknown")throw new OperationsProblem(409,"ReconciliationRequiresUnknown");
   row.NextReconcileAtUtc=clock.GetUtcNow();row.ReconcileAttempts=0;row.Version=checked(row.Version+1);
   db.Audit.Add(new(){Id=Guid.NewGuid(),Actor=OperationsRegistration.Actor(http),Action="Reconcile "+id,OccurredAtUtc=clock.GetUtcNow()});
   await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);return Results.Json(new{id,status=row.Status,simulated=true},statusCode:202);
  });
 }
}
