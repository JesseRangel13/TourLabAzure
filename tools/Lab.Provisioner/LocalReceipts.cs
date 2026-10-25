using System.Text.Json;
using Azure.Storage.Blobs.Models;
using TourLab.Azure.Notifications.Receipts;
namespace TourLab.Azure.Provisioning;

public static class LocalReceipts
{
    public static async Task InitializeAsync(string root, string output, CancellationToken ct)
    {
        var folder = Path.GetFullPath(output);
        if (!folder.StartsWith(Path.Combine(Path.GetFullPath(root), ".local") + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Only this repository's private local profile is permitted.");
        var path = Path.Combine(folder, "notifications.json");
        var account = File.ReadLines(Path.Combine(root, ".local", "dependencies.env"))
            .Single(x => x.StartsWith("AZURELAB_AZURITE_ACCOUNTS=", StringComparison.Ordinal))["AZURELAB_AZURITE_ACCOUNTS=".Length..].Split(':');
        if (account.Length != 2 || account[0] != "tourlabazure") throw new InvalidOperationException("Dedicated lab account required.");
        var storage = $"DefaultEndpointsProtocol=http;AccountName={account[0]};AccountKey={account[1]};BlobEndpoint=http://127.0.0.1:10010/{account[0]};QueueEndpoint=http://127.0.0.1:10011/{account[0]};TableEndpoint=http://127.0.0.1:10012/{account[0]};";
        var options = new ReceiptStorageOptions { Enabled = true, Local = true, ConnectionString = storage };
        var container = BlobReceiptStorage.Create(options);
        await container.CreateIfNotExistsAsync(PublicAccessType.None, cancellationToken: ct);
        if ((await container.GetAccessPolicyAsync(cancellationToken: ct)).Value.BlobPublicAccess != PublicAccessType.None)
            throw new InvalidOperationException("Existing receipt container is public; refusing to use it.");
        var settings = JsonSerializer.Deserialize<Dictionary<string,JsonElement>>(File.ReadAllText(path))!;
        settings["ReceiptStorage"] = JsonSerializer.SerializeToElement(options);
        File.WriteAllText(path, JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }));
        // Supply only Notifications runtime SQL and receipt storage to the worker, never migrator or JWT keys.
        var worker = new {
            ConnectionStrings = settings["ConnectionStrings"],
            ReceiptStorage = options
        };
        File.WriteAllText(Path.Combine(folder, "receipts.json"), JsonSerializer.Serialize(worker, new JsonSerializerOptions { WriteIndented = true }));
        // Functions host storage is separately configured; only host coordination data lives here.
        var host = new Dictionary<string,string> {
            ["AzureWebJobsStorage"] = storage,
            ["FUNCTIONS_WORKER_RUNTIME"] = "dotnet-isolated",
            ["AZURE_FUNCTIONS_ENVIRONMENT"] = "Development",
            ["DOTNET_ENVIRONMENT"] = "Development",
            ["AzureFunctionsWebHost__hostid"] = "tourlab-azure-receipts",
            ["ReceiptBus"] = "Endpoint=sb://localhost:5673;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;"
        };
        File.WriteAllText(Path.Combine(folder, "receipts-host.json"), JsonSerializer.Serialize(host, new JsonSerializerOptions { WriteIndented = true }));
    }
}