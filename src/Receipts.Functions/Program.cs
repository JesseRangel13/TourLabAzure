using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TourLab.Azure.Infrastructure.Persistence;
using TourLab.Azure.Notifications.Persistence;
using TourLab.Azure.Notifications.Receipts;

var builder = FunctionsApplication.CreateBuilder(args);
var privatePath = Environment.GetEnvironmentVariable("AZURELAB_RECEIPTS_CONFIG");
if (!string.IsNullOrWhiteSpace(privatePath))
    builder.Configuration.AddJsonFile(Path.GetFullPath(privatePath), optional: false, reloadOnChange: false);
builder.Configuration.AddEnvironmentVariables("AZURELAB_");
// Retain the Functions gRPC logging provider; clearing it hides worker logs from Core Tools.
builder.Logging.AddFilter("TourLab.Azure", LogLevel.Information);
builder.Logging.AddJsonConsole(o => o.IncludeScopes = true);
builder.Logging.Configure(o => o.ActivityTrackingOptions = ActivityTrackingOptions.TraceId | ActivityTrackingOptions.SpanId);
builder.Logging.AddFilter("Microsoft.EntityFrameworkCore.Database.Command", LogLevel.Warning);
builder.Logging.AddFilter("Microsoft.EntityFrameworkCore.Update", LogLevel.None);
builder.Services.AddOptions<TourLab.Azure.Receipts.ReceiptBusSettings>()
    .Configure<IConfiguration>( (o, c) => o.ConnectionString = c["ReceiptBus"] ?? "")
    .Validate(o => TourLab.Azure.Receipts.ReceiptBusSettings.Safe(o.ConnectionString, builder.Environment.IsDevelopment()), "Only the explicitly enabled local receipt broker is supported until cloud configuration is verified.")
    .ValidateOnStart();
builder.Services.AddRuntimeDatabase("notifications");
builder.Services.AddDbContext<NotificationsDb>((services, options) => options.UseSqlServer(
    services.GetRequiredService<IOptions<RuntimeDatabase>>().Value.ConnectionString,
    sql => { sql.MigrationsHistoryTable("__EFMigrationsHistory", "notifications"); sql.CommandTimeout(5); }));
builder.Services.AddOptions<ReceiptStorageOptions>().Bind(builder.Configuration.GetSection("ReceiptStorage"))
    .Validate(o => o.Enabled && BlobReceiptStorage.Safe(o, builder.Environment.IsDevelopment()), "Unsafe receipt storage").ValidateOnStart();
builder.Services.AddSingleton<IReceiptStorage, BlobReceiptStorage>();
builder.Services.AddScoped<ReceiptProcessor>();
builder.Services.AddSingleton<IReceiptInvocationHook, TourLab.Azure.Receipts.LocalReceiptBarrier>();
await builder.Build().RunAsync();