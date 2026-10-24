using System.Text.Json;
using Azure.Storage.Blobs.Models;
using TourLab.Azure.Catalog.Images;
namespace TourLab.Azure.Provisioning;

public static class LocalImages
{
    public static async Task InitializeAsync(string root,string output,CancellationToken ct)
    {
        var folder=Path.GetFullPath(output);
        if(!folder.StartsWith(Path.Combine(Path.GetFullPath(root),".local")+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Only this repository's private local profile is permitted.");
        var path=Path.Combine(folder,"catalog.json");
        var account=File.ReadLines(Path.Combine(root,".local","dependencies.env")).Single(x=>x.StartsWith("AZURELAB_AZURITE_ACCOUNTS=",StringComparison.Ordinal))["AZURELAB_AZURITE_ACCOUNTS=".Length..].Split(':');
        if(account.Length!=2||account[0]!="tourlabazure")throw new InvalidOperationException("Dedicated Azure lab storage account required.");
        var options=new ImageStorageOptions {Enabled=true,Local=true,ConnectionString=$"DefaultEndpointsProtocol=http;AccountName={account[0]};AccountKey={account[1]};BlobEndpoint=http://127.0.0.1:10010/{account[0]};"};
        var container=BlobImageStorage.Create(options);
        await container.CreateIfNotExistsAsync(PublicAccessType.None,cancellationToken:ct);
        if((await container.GetAccessPolicyAsync(cancellationToken:ct)).Value.BlobPublicAccess!=PublicAccessType.None)
            throw new InvalidOperationException("Existing image container is public; refusing to use it.");
        var settings=JsonSerializer.Deserialize<Dictionary<string,JsonElement>>(File.ReadAllText(path))!;
        settings["ImageStorage"]=JsonSerializer.SerializeToElement(options);
        File.WriteAllText(path,JsonSerializer.Serialize(settings,new JsonSerializerOptions{WriteIndented=true}));
    }
}
