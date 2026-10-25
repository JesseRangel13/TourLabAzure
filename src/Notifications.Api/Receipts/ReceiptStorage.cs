using Azure;
using Azure.Identity;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Options;
namespace TourLab.Azure.Notifications.Receipts;

public sealed class ReceiptStorageOptions
{
    public bool Enabled { get; set; }
    public bool Local { get; set; }
    public string ConnectionString { get; set; } = "";
    public string ServiceUri { get; set; } = "";
    public string Container { get; set; } = "tourlab-azure-booking-receipts";
}
public sealed class ReceiptObjectConflict : Exception;
public interface IReceiptStorage
{
    Task EnsureAsync(string key, byte[] bytes, string hash, CancellationToken ct);
    Task<byte[]> ReadAsync(string key, string hash, CancellationToken ct);
}
public sealed class BlobReceiptStorage(IOptions<ReceiptStorageOptions> settings) : IReceiptStorage
{
    private readonly Lazy<BlobContainerClient> client = new(() => Create(settings.Value));
    public static bool Safe(ReceiptStorageOptions o, bool development)
    {
        if (!o.Enabled) return true;
        if (o.Container != "tourlab-azure-booking-receipts") return false;
        try
        {
            if (o.Local) return development && string.IsNullOrEmpty(o.ServiceUri) &&
                new BlobServiceClient(o.ConnectionString).Uri.AbsoluteUri.TrimEnd('/') == "http://127.0.0.1:10010/tourlabazure";
            return string.IsNullOrEmpty(o.ConnectionString) && Uri.TryCreate(o.ServiceUri, UriKind.Absolute, out var cloud) &&
                cloud.Scheme == "https" && cloud.Host.StartsWith("tourlabazure", StringComparison.Ordinal) &&
                cloud.Host.EndsWith(".blob.core.windows.net", StringComparison.Ordinal) &&
                cloud.AbsolutePath == "/" && string.IsNullOrEmpty(cloud.UserInfo) && string.IsNullOrEmpty(cloud.Query);
        }
        catch (Exception e) when (e is ArgumentException or FormatException) { return false; }
    }
    public static BlobContainerClient Create(ReceiptStorageOptions o)
    {
        var options = new BlobClientOptions(BlobClientOptions.ServiceVersion.V2025_01_05);
        options.Retry.MaxRetries = 0; options.Retry.NetworkTimeout = TimeSpan.FromSeconds(10);
        return o.Local ? new(o.ConnectionString, o.Container, options) :
            new BlobServiceClient(new Uri(o.ServiceUri), new ManagedIdentityCredential(ManagedIdentityId.SystemAssigned), options).GetBlobContainerClient(o.Container);
    }
    private BlobClient Blob(string key)
    {
        if (!settings.Value.Enabled) throw new InvalidOperationException("ReceiptStorageDisabled");
        return client.Value.GetBlobClient(key);
    }
    public async Task EnsureAsync(string key, byte[] bytes, string hash, CancellationToken ct)
    {
        if (bytes.Length > ReceiptDocument.MaximumBytes || ReceiptDocument.Hash(bytes) != hash) throw new ReceiptObjectConflict();
        try
        {
            await Blob(key).UploadAsync(new BinaryData(bytes), new BlobUploadOptions {
                Conditions = new BlobRequestConditions { IfNoneMatch = ETag.All },
                HttpHeaders = new BlobHttpHeaders { ContentType = "text/plain; charset=utf-8" },
                Metadata = new Dictionary<string,string> { ["sha256"] = hash }
            }, ct);
        }
        catch (RequestFailedException e) when (e.Status is 409 or 412) { }
        // Verify actual bytes even on duplicates; metadata alone is not a content guarantee.
        var actual = await ReadAsync(key, hash, ct);
        if (!actual.AsSpan().SequenceEqual(bytes)) throw new ReceiptObjectConflict();
    }
    public async Task<byte[]> ReadAsync(string key, string hash, CancellationToken ct)
    {
        var blob = Blob(key);
        var properties = (await blob.GetPropertiesAsync(cancellationToken: ct)).Value;
        if (properties.ContentLength > ReceiptDocument.MaximumBytes ||
            properties.ContentType != "text/plain; charset=utf-8" ||
            !properties.Metadata.TryGetValue("sha256", out var storedHash) || storedHash != hash)
            throw new ReceiptObjectConflict();
        var response = await blob.DownloadStreamingAsync(new BlobDownloadOptions {
            Conditions = new BlobRequestConditions { IfMatch = properties.ETag }
        }, ct);
        await using var stream = response.Value.Content;
        using var bytes = new MemoryStream();
        var buffer = new byte[4096];
        int read;
        while ((read = await stream.ReadAsync(buffer, ct)) != 0)
        {
            if (bytes.Length + read > ReceiptDocument.MaximumBytes) throw new ReceiptObjectConflict();
            bytes.Write(buffer, 0, read);
        }
        var result = bytes.ToArray();
        if (ReceiptDocument.Hash(result) != hash) throw new ReceiptObjectConflict();
        return result;
    }
}