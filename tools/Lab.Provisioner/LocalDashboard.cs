using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using TourLab.Azure.Reservations.Dashboard;
namespace TourLab.Azure.Provisioning;

public static class LocalDashboard
{
    public static async Task InitializeAsync(string root,string output,CancellationToken ct)
    {
        root=Path.GetFullPath(root);var directory=Path.GetFullPath(output);
        if(!directory.StartsWith(Path.Combine(root,".local")+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Only the Azure lab private profile is permitted.");
        var path=Path.Combine(directory,"reservations.json");
        var config=JsonSerializer.Deserialize<Dictionary<string,JsonElement>>(File.ReadAllText(path))!;
        DashboardOptions options;
        if(config.TryGetValue("Dashboard",out var existing))options=existing.Deserialize<DashboardOptions>()!;
        else
        {
            using var key=RSA.Create(3072);
            var request=new CertificateRequest("CN=TourLab Azure Dashboard Keys",key,HashAlgorithmName.SHA256,RSASignaturePadding.Pkcs1);
            using var cert=request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-5),DateTimeOffset.UtcNow.AddYears(1));
            var password="Az7!"+Convert.ToHexString(RandomNumberGenerator.GetBytes(24));
            var certPath=Path.Combine(directory,"dashboard-protection.pfx");
            File.WriteAllBytes(certPath,cert.Export(X509ContentType.Pfx,password));
            options=new DashboardOptions{
                Enabled=true,Local=true,Endpoint="https://localhost:8089/",
                Key=File.ReadAllText(Path.Combine(root,".local","cosmos-account.key")).Trim(),
                CaPath=Path.Combine(root,".local","certificates","ca.crt"),
                KeyDirectory=Path.Combine(directory,"dashboard-keys"),
                CertificatePath=certPath,CertificatePassword=password
            };
        }
        if(!DashboardRules.Safe(options,true))throw new InvalidOperationException("Unsafe Cosmos profile.");
        var connection=CosmosBookingProjection.Create(options);
        using var client=connection.Client;using var http=connection.Http;
        var database=await client.CreateDatabaseIfNotExistsAsync(options.Database,cancellationToken:ct);
        var container=await database.Database.CreateContainerIfNotExistsAsync(DashboardRules.Properties(),cancellationToken:ct);
        var properties=(await container.Container.ReadContainerAsync(cancellationToken:ct)).Resource;
        if(properties.PartitionKeyPath!="/userId"||properties.DefaultTimeToLive is not null)
            throw new InvalidOperationException("Existing booking container has unsafe partition or TTL.");
        config["Dashboard"]=JsonSerializer.SerializeToElement(options);
        File.WriteAllText(path,JsonSerializer.Serialize(config,new JsonSerializerOptions{WriteIndented=true}));
    }
}
