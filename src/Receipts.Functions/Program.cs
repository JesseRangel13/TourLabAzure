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
await TourLab.Azure.Infrastructure.Security.StartupSecrets.LoadAsync(builder.Configuration, builder.Environment.IsDevelopment(), "receipts", CancellationToken.None);
builder.Configuration.AddInMemoryCollection(new Dictionary<string,string?> { ["AzureSecurity:Workload"] = "receipts" });
// Retain the Functions gRPC logging provider; clearing it hides worker logs from Core Tools.
builder.Logging.AddFilter("TourLab.Azure", LogLevel.Information);
builder.Logging.AddJsonConsole(o => o.IncludeScopes = true);
builder.Logging.Configure(o => o.ActivityTrackingOptions = ActivityTrackingOptions.TraceId | ActivityTrackingOptions.SpanId);
builder.Logging.AddFilter("Microsoft.EntityFrameworkCore.Database.Command", LogLevel.Warning);
builder.Logging.AddFilter("Microsoft.EntityFrameworkCore.Update", LogLevel.None);
builder.Services.AddOptions<TourLab.Azure.Receipts.ReceiptBusSettings>()
    .Configure<IConfiguration>( (o, c) => { o.ConnectionString = c["ReceiptBus"] ?? ""; o.LocalTestBroker = c.GetValue<bool>("ReceiptTestBroker"); o.Cloud = TourLab.Azure.Infrastructure.Security.AzureSecurity.Cloud(c); })
    .Validate(o => TourLab.Azure.Receipts.ReceiptBusSettings.Safe(o.ConnectionString, builder.Environment.IsDevelopment(), o.LocalTestBroker) || o.Cloud, "Receipt binding requires validated local emulator or explicit managed identity settings.")
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
