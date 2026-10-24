using System.Security.AccessControl;
using System.Security.Principal;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using TourLab.Azure.Infrastructure.Persistence;

namespace TourLab.Azure.Provisioning;

public sealed record SeedUser(string UserName, string Password, string Role);
public sealed class ProvisioningConfiguration
{
    public string Database { get; set; } = "";
    public string Profile { get; set; } = "";
    public string Admin { get; set; } = "";
    public Dictionary<string, string> Runtime { get; set; } = [];
    public Dictionary<string, string> Migrator { get; set; } = [];
    public SeedUser[] Users { get; set; } = [];
}

public static class PrivateConfiguration
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    public static ProvisioningConfiguration Read(string path) =>
        JsonSerializer.Deserialize<ProvisioningConfiguration>(File.ReadAllText(path), Json) ?? throw new InvalidOperationException("Private configuration is invalid.");

    public static void Initialize(string root, string output, string database, string profile)
    {
        root = Path.GetFullPath(root);
        output = Path.GetFullPath(output);
        if (!output.StartsWith(Path.Combine(root, ".local") + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
            !System.Text.RegularExpressions.Regex.IsMatch(database, @"^tourlab_azure(?:_[a-zA-Z0-9]+)*$") ||
            profile is not ("LocalDb" or "Docker"))
            throw new ArgumentException("Only an isolated .local output and lab database/profile are allowed.");
        if (Directory.Exists(output)) throw new InvalidOperationException("Private output exists; reuse it without credential rotation.");
        Directory.CreateDirectory(output);
        if (OperatingSystem.IsWindows())
        {
            var security = new DirectorySecurity();
            security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
            security.AddAccessRule(new FileSystemAccessRule(WindowsIdentity.GetCurrent().User!, FileSystemRights.FullControl,
                InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow));
            new DirectoryInfo(output).SetAccessControl(security);
        }
        else File.SetUnixFileMode(output, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        var server = profile == "LocalDb" ? @"(localdb)\tourlab-azure" : "127.0.0.1,14339";
        var admin = new SqlConnectionStringBuilder
        {
            DataSource = server, InitialCatalog = "master", ConnectTimeout = 5, Pooling = true, MaxPoolSize = 5,
            IntegratedSecurity = profile == "LocalDb", Encrypt = profile == "LocalDb" ? SqlConnectionEncryptOption.Optional : SqlConnectionEncryptOption.Mandatory,
            TrustServerCertificate = profile == "Docker"
        };
        if (profile == "Docker")
        {
            var line = File.ReadLines(Path.Combine(root, ".local", "dependencies.env"))
                .Single(value => value.StartsWith("AZURELAB_SQL_PASSWORD=", StringComparison.Ordinal));
            admin.UserID = "sa";
            admin.Password = line["AZURELAB_SQL_PASSWORD=".Length..];
        }
        var config = new ProvisioningConfiguration
        {
            Database = database, Profile = profile, Admin = admin.ConnectionString,
            Users = [new("Alice", Secret(), "Tourist"), new("Bob", Secret(), "Tourist"), new("Admin", Secret(), "Admin")]
        };
        foreach (var schema in SqlRules.Schemas)
        {
            config.Runtime[schema] = Connection(admin, database, $"tourlab_azure_{schema}");
            config.Migrator[schema] = Connection(admin, database, $"tourlab_azure_{schema}_migrator");
        }
        using var human = RSA.Create(3072);
        using var quote = RSA.Create(3072);
        File.WriteAllText(Path.Combine(output, "human-signing.key"), human.ExportPkcs8PrivateKeyPem());
        File.WriteAllText(Path.Combine(output, "human-signing.pub"), human.ExportSubjectPublicKeyInfoPem());
        File.WriteAllText(Path.Combine(output, "quote-signing.key"), quote.ExportPkcs8PrivateKeyPem());
        File.WriteAllText(Path.Combine(output, "quote-signing.pub"), quote.ExportSubjectPublicKeyInfoPem());
        using var dpKey = RSA.Create(3072);
        var request = new CertificateRequest("CN=TourLab Azure Data Protection", dpKey, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow.AddYears(1));
        var dpPassword = Secret();
        File.WriteAllBytes(Path.Combine(output, "data-protection.pfx"), certificate.Export(X509ContentType.Pfx, dpPassword));
        foreach (var schema in SqlRules.Schemas)
        {
            var settings = new Dictionary<string, object?>
            {
                ["ConnectionStrings"] = new { Runtime = config.Runtime[schema] },
                ["AllowedHosts"] = "localhost;127.0.0.1",
                ["Database"] = new { Profile = profile },
                ["Jwt"] = schema == "identity"
                    ? new { PrivateKeyPath = Path.Combine(output, "human-signing.key") } as object
                    : new { PublicKeyPath = Path.Combine(output, "human-signing.pub") },
                ["Quote"] = schema == "reservations"
                    ? new { PrivateKeyPath = Path.Combine(output, "quote-signing.key") } as object
                    : new { PublicKeyPath = Path.Combine(output, "quote-signing.pub") }
            };
            if (schema == "reservations") { settings["Catalog"] = new { BaseAddress = "https://localhost:9444/" }; settings["Tls"] = new { CaPath = Path.Combine(root, ".local", "certificates", "ca.crt") }; }
            if (schema == "identity")
            {
                settings["DataProtection"] = new { CertificatePath = Path.Combine(output, "data-protection.pfx"), CertificatePassword = dpPassword };
                var caPath = Path.Combine(root, ".local", "certificates", "ca.crt");
                if (File.Exists(caPath)) settings["Tls"] = new { CaPath = caPath };
                settings["Browser"] = new { AllowedOrigins = new[] { "https://localhost:9443", "https://localhost:4201" } };
                settings["Backends"] = new Dictionary<string, string>
                {
                    ["catalog"] = "https://localhost:9444/", ["reservations"] = "https://localhost:9445/",
                    ["payments"] = "https://localhost:9446/", ["notifications"] = "https://localhost:9447/"
                };
            }
            var service = schema == "identity" ? "gateway" : schema;
            File.WriteAllText(Path.Combine(output, $"{service}.json"), JsonSerializer.Serialize(settings, Json));
        }
        File.WriteAllText(Path.Combine(output, "provisioner.json"), JsonSerializer.Serialize(config, Json));
    }

    private static string Connection(SqlConnectionStringBuilder admin, string database, string user)
    {
        var connection = new SqlConnectionStringBuilder(admin.ConnectionString)
        {
            IntegratedSecurity = false, InitialCatalog = database, UserID = user, Password = Secret()
        };
        return connection.ConnectionString;
    }
    private static string Secret() => "Az2!" + Convert.ToHexString(RandomNumberGenerator.GetBytes(24));
}
