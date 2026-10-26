using System.Net;
using System.Net.Sockets;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Security.Cryptography;
using Microsoft.Extensions.Configuration;
using TourLab.Azure.Infrastructure.Security;
namespace TourLab.Azure.Provisioning;
public static class InfrastructureParameters
{
    public static void ValidateFile(string file) => Validate(JsonNode.Parse(File.ReadAllText(file))?["parameters"]?.AsObject()??throw new ArgumentException("ARM parameters required."));
    public static void Validate(JsonObject parameters)
    {
        JsonNode Value(string name)=>parameters[name]?["value"]??throw new ArgumentException("Required parameter missing.");
        string Text(string name)=>Value(name).GetValue<string>();
        void Require(bool value){if(!value)throw new ArgumentException("Infrastructure parameter boundary rejected.");}
        Require(Regex.IsMatch(Text("suffix"),"^[a-z0-9]{4,6}$")&&Text("location")=="eastus"&&!string.IsNullOrWhiteSpace(Text("owner")));
        foreach(var name in new[]{"sqlAdminObjectId","sqlAdminTenantId"})Require(Guid.TryParse(Text(name),out var id)&&id!=Guid.Empty);
        Require(Regex.IsMatch(Text("githubRepository"),"^[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+$")&&!Text("githubRepository").Contains("REPLACE",StringComparison.Ordinal));
        Require(Text("apiSku") is "B2" or "B3");
        Require(Value("budgetUsd").GetValue<int>()>0&&Value("budgetEmails").AsArray().Count>0);
        Require(Regex.IsMatch(Text("budgetStartDate"),"^20[0-9]{2}-[0-9]{2}-01T00:00:00Z$"));
        var ips=Value("sqlFirewall").AsArray();Require(ips.Count<=64);
        foreach(var node in ips){
            Require(IPAddress.TryParse(node?.GetValue<string>(),out var ip)&&ip.AddressFamily==AddressFamily.InterNetwork&&!IPAddress.IsLoopback(ip)&&!ip.Equals(IPAddress.Any));
        }
        foreach(var workload in new[]{"gateway","catalog","reservations","payments","notifications"}){
            var proxies=Value("knownProxies")[workload]?.AsArray()??throw new ArgumentException("Exact proxies required.");
            var config=new ConfigurationManager();
            config.AddInMemoryCollection(proxies.Select((node,i)=>KeyValuePair.Create<string,string?>("Ingress:KnownProxies:"+i,node?.GetValue<string>())));
            _=CloudIngress.Options(config);
        }
        foreach(var kind in new[]{"human","quote"}){
            var config=new ConfigurationManager();config["AzureSecurity:Mode"]="ManagedIdentity";
            var keys=Value(kind+"PublicKeys").AsArray();
            foreach(var (node,i) in keys.Select((n,i)=>(n,i)))config["Jwt:ValidationKeys:"+i]=node?.GetValue<string>();
            var validated=SigningMaterial.Public(config,"Jwt");
            Require(validated.Any(key=>key.KeyId==Text(kind+"SigningFingerprint")));
            Require(Regex.IsMatch(Text(kind+"SecretVersion"),"^[a-f0-9]{32}$"));
        }
    }
}
