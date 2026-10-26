using System.Security.Cryptography;
using System.Text.Json.Nodes;
using TourLab.Azure.Provisioning;
using TourLab.Azure.Infrastructure.Security;
namespace TourLab.Azure.Tests;
public sealed class InfrastructureParameterTests
{
    private static readonly Lazy<JsonObject> Prepared=new(()=>{
        using var human=RSA.Create(3072);using var quote=RSA.Create(3072);
        var values=new Dictionary<string,object>{
            ["suffix"]="test11",["location"]="eastus",["owner"]="offline-test",
            ["sqlAdminObjectId"]=Guid.NewGuid().ToString(),["sqlAdminTenantId"]=Guid.NewGuid().ToString(),
            ["githubRepository"]="lab/azure",["apiSku"]="B2",["budgetUsd"]=500,["budgetEmails"]=new[]{"study@example.invalid"},["budgetStartDate"]="2026-10-01T00:00:00Z",
            ["configureApplications"]=true,["sqlFirewall"]=new[]{"203.0.113.1"},
            ["knownProxies"]=new[]{"gateway","catalog","reservations","payments","notifications"}.ToDictionary(w=>w,w=>new[]{"10.10.0.4"}),
            ["humanPublicKeys"]=new[]{human.ExportSubjectPublicKeyInfoPem()},["quotePublicKeys"]=new[]{quote.ExportSubjectPublicKeyInfoPem()},
            ["humanSigningFingerprint"]=SigningMaterial.KeyId(human),["quoteSigningFingerprint"]=SigningMaterial.KeyId(quote),
            ["humanSecretVersion"]=new string('a',32),["quoteSecretVersion"]=new string('b',32)
        };
        var result=new JsonObject();foreach(var (key,value) in values)result[key]=new JsonObject{["value"]=System.Text.Json.JsonSerializer.SerializeToNode(value)};return result;
    });
    [Fact]
    public void Reviewed_offline_metadata_uses_no_network_or_private_signing_material() => InfrastructureParameters.Validate((JsonObject)Prepared.Value.DeepClone());
    [Theory]
    [InlineData("suffix","../foreign")]
    [InlineData("location","otherregion")]
    [InlineData("sqlAdminObjectId","00000000-0000-0000-0000-000000000000")]
    [InlineData("humanSecretVersion","latest")]
    [InlineData("humanSigningFingerprint","AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    [InlineData("apiSku","P3v3")]
    public void Unreviewed_target_runtime_or_signer_metadata_fails_closed(string key,string bad)
    {
        var p=(JsonObject)Prepared.Value.DeepClone();p[key]!["value"]=bad;
        Assert.ThrowsAny<ArgumentException>(()=>InfrastructureParameters.Validate(p));
    }
    [Fact]
    public void Sql_allow_all_firewall_is_rejected()
    {
        var p=(JsonObject)Prepared.Value.DeepClone();p["sqlFirewall"]!["value"]=new JsonArray("0.0.0.0");
        Assert.Throws<ArgumentException>(()=>InfrastructureParameters.Validate(p));
    }
}
