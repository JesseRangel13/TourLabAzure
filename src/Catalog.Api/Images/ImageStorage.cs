using Azure;
using Azure.Identity;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Options;
using TourLab.Azure.Catalog.Application;

namespace TourLab.Azure.Catalog.Images;

public sealed class ImageStorageOptions
{
    public bool Enabled { get; set; }
    public bool Local { get; set; }
    public string ConnectionString { get; set; } = "";
    public string ServiceUri { get; set; } = "";
    public string Container { get; set; } = "tourlab-azure-tour-images";
}
public sealed record StoredImage(long Size, string? Sha256, string ContentType);
public interface IImageStorage
{
    Task PutAsync(string key, Stream content, string type, string hash, CancellationToken ct);
    Task<StoredImage?> HeadAsync(string key, CancellationToken ct);
    Task DeleteAsync(string key, CancellationToken ct);
    Task<Stream> OpenAsync(string key, CancellationToken ct);
}
// Singleton SDK client; retries are owned by the durable SQL workflow.
public sealed class BlobImageStorage(IOptions<ImageStorageOptions> settings) : IImageStorage
{
    private readonly Lazy<BlobContainerClient> client = new(() => Create(settings.Value));
    public static bool Safe(ImageStorageOptions o, bool development)
    {
        if (!o.Enabled) return true;
        if (o.Container != "tourlab-azure-tour-images") return false;
        try
        {
            if (o.Local)
            {
                if (!development || !string.IsNullOrEmpty(o.ServiceUri)) return false;
                var uri = new BlobServiceClient(o.ConnectionString).Uri;
                return uri.AbsoluteUri.TrimEnd('/') == "http://127.0.0.1:10010/tourlabazure";
            }
            return string.IsNullOrEmpty(o.ConnectionString) && Uri.TryCreate(o.ServiceUri, UriKind.Absolute, out var cloud)
                && cloud.Scheme == "https" && cloud.Host.StartsWith("tourlabazure", StringComparison.Ordinal)
                && cloud.Host.EndsWith(".blob.core.windows.net", StringComparison.Ordinal)
                && cloud.AbsolutePath == "/" && string.IsNullOrEmpty(cloud.UserInfo) && string.IsNullOrEmpty(cloud.Query);
        }
        catch (Exception error) when (error is ArgumentException or FormatException) { return false; }
    }
    public static BlobContainerClient Create(ImageStorageOptions o)
    {
        var options = new BlobClientOptions(BlobClientOptions.ServiceVersion.V2025_01_05);
        options.Retry.MaxRetries = 0; options.Retry.NetworkTimeout = TimeSpan.FromSeconds(15);
        return o.Local ? new BlobContainerClient(o.ConnectionString, o.Container, options)
            : new BlobServiceClient(new Uri(o.ServiceUri), new ManagedIdentityCredential(ManagedIdentityId.SystemAssigned), options).GetBlobContainerClient(o.Container);
    }
    private BlobClient Blob(string key)
    {
        if (!settings.Value.Enabled) throw new CatalogProblem(503, "ImageStorageDisabled");
        return client.Value.GetBlobClient(key);
    }
    public async Task PutAsync(string key, Stream content, string type, string hash, CancellationToken ct)
    {
        try
        {
            await Blob(key).UploadAsync(content, new BlobUploadOptions {
                Conditions = new BlobRequestConditions { IfNoneMatch = ETag.All },
                HttpHeaders = new BlobHttpHeaders { ContentType = type },
                Metadata = new Dictionary<string,string> { ["sha256"] = hash },
                TransferOptions = new global::Azure.Storage.StorageTransferOptions { MaximumConcurrency = 1, InitialTransferSize = ValidatedImage.MaximumBytes, MaximumTransferSize = ValidatedImage.MaximumBytes }
            }, ct);
        }
        catch (RequestFailedException e) when (e.Status is 409 or 412)
        {
            var existing = await HeadAsync(key, ct);
            if (existing != new StoredImage(content.Length, hash, type)) throw new CatalogProblem(409, "ImageObjectConflict");
        }
    }
    public async Task<StoredImage?> HeadAsync(string key, CancellationToken ct)
    {
        try
        {
            var p = (await Blob(key).GetPropertiesAsync(cancellationToken: ct)).Value;
            return new(p.ContentLength, p.Metadata.TryGetValue("sha256", out var hash) ? hash : null, p.ContentType);
        }
        catch (RequestFailedException e) when (e.Status == 404) { return null; }
    }
    public async Task DeleteAsync(string key, CancellationToken ct) =>
        await Blob(key).DeleteIfExistsAsync(cancellationToken: ct);
    public async Task<Stream> OpenAsync(string key, CancellationToken ct) =>
        (await Blob(key).DownloadStreamingAsync(cancellationToken: ct)).Value.Content;
}
