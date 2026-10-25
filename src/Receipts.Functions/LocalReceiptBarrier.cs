using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using TourLab.Azure.Notifications.Receipts;
namespace TourLab.Azure.Receipts;

// Deterministic process-crash test barrier, enabled only through private local configuration.
// No HTTP crash endpoint; production refuses the setting.
public sealed class LocalReceiptBarrier : IReceiptInvocationHook
{
    private readonly Guid? job;
    private readonly string marker;
    public LocalReceiptBarrier(IConfiguration config, IHostEnvironment environment)
    {
        marker = "";
        var value = config["ReceiptTestBarrier:JobId"];
        if (string.IsNullOrEmpty(value)) return;
        var privateFile = Environment.GetEnvironmentVariable("AZURELAB_RECEIPTS_CONFIG") ?? "";
        var directory = Path.GetDirectoryName(Path.GetFullPath(privateFile))!;
        var root = new DirectoryInfo(environment.ContentRootPath);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "TourLab.Azure.slnx"))) root = root.Parent;
        if (!environment.IsDevelopment() || root is null ||
            !directory.StartsWith(Path.Combine(root.FullName, ".local") + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Receipt test barrier requires this lab's private Development profile.");
        job = Guid.Parse(value);
        marker = Path.Combine(directory, "receipt-after-blob.marker");
    }
    public async Task AfterBlobAsync(Guid id, CancellationToken ct)
    {
        if (job != id) return;
        await File.WriteAllTextAsync(marker, id.ToString("D"), ct);
        await Task.Delay(Timeout.InfiniteTimeSpan, ct);
    }
}