using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TourLab.Azure.Infrastructure.Security;
namespace TourLab.Azure.Infrastructure.Operations;
public static class OperationsRegistration {
 public static void AddServiceOperations(this IServiceCollection services,IConfiguration config,IHostEnvironment environment,string schema){
  if(schema is not ("catalog" or "reservations" or "payments" or "notifications"))throw new ArgumentException("Invalid operations owner.");
  services.AddSingleton(sp=>new OperationsOwner(schema,sp.GetRequiredService<IHostEnvironment>().IsDevelopment()&&sp.GetRequiredService<IConfiguration>().GetValue<bool>("LabFeaturesEnabled")));
  services.AddScoped<ServiceOperations>();services.AddSingleton<WorkerStatus>();
  services.AddRateLimiter(o=>{o.RejectionStatusCode=429;o.AddPolicy("operations-admin",http=>RateLimitPartition.GetFixedWindowLimiter(http.User.FindFirstValue("sub")??"anonymous",
   _=>new FixedWindowRateLimiterOptions{PermitLimit=60,Window=TimeSpan.FromMinutes(1),QueueLimit=0}));});
 }
 public static void MapServiceOperations(this WebApplication app,string schema){
  app.UseRateLimiter();
  var admin=app.MapGroup("/v1/admin").RequireAuthorization(BackendSecurity.Admin).RequireRateLimiting("operations-admin").AddEndpointFilter(async(context,next)=>{
   try{return await next(context);}catch(OperationsProblem error){return Results.Problem(statusCode:error.Status,title:error.Code,extensions:new Dictionary<string,object?>{["code"]=error.Code});}
  });
  admin.MapGet("/diagnostics",(ServiceOperations operations,int? page,int? pageSize,CancellationToken ct)=>operations.DiagnosticsAsync(page??1,pageSize??10,ct));
  admin.MapGet("/dead-letters",(ServiceOperations operations,HttpContext http,string? source,int? pageSize,long? fromSequence,CancellationToken ct)=>operations.InspectAsync(source??"commands",pageSize??10,fromSequence,Actor(http),ct));
  admin.MapPost("/dead-letters/{id:guid}/replay",(Guid id,ServiceOperations operations,HttpContext http,CancellationToken ct)=>operations.ReplayAsync(id,Actor(http),ct));
  admin.MapGet("/faults",(ServiceOperations operations,HttpContext http,CancellationToken ct)=>operations.FaultAsync(null,Actor(http),ct));
  admin.MapPut("/faults",(FaultSelection input,ServiceOperations operations,HttpContext http,CancellationToken ct)=>operations.FaultAsync(input,Actor(http),ct));
  admin.MapPost("/faults/reset",(ServiceOperations operations,HttpContext http,CancellationToken ct)=>operations.FaultAsync(new(schema=="payments"?"Success":"None",0),Actor(http),ct));
 }
 public static string Actor(HttpContext http)=>http.User.FindFirstValue("sub")??throw new OperationsProblem(401,"InvalidIdentity");
}
