using System.Net;
using System.Text;
using System.Text.Json;
using Azure;
using Azure.Core;
using Azure.Core.Pipeline;
using Azure.Security.KeyVault.Secrets;
using TourLab.Azure.Infrastructure.Security;
namespace TourLab.Azure.Tests;

// SDK + mocked HTTPS transport only. These tests do not establish real Key Vault or managed identity access.
public sealed class VaultSdkTests
{
    [Fact]
    public async Task ActualSecretSdkRequestsOnlyThePinnedVersionAndUsesTheVaultTokenScope()
    {
        var version=new string('1',32);var name="tourlab-azure-human-signing";var value="synthetic-unit-secret";
        var calls=new List<Uri>();var credential=new Credential();
        using var http=new HttpClient(new Handler((request,ct)=>{
            calls.Add(request.RequestUri!);Assert.Equal(HttpMethod.Get,request.Method);
            if(request.Headers.Authorization is null)return new(HttpStatusCode.Unauthorized) {
                Headers={WwwAuthenticate={new("Bearer","authorization=\"https://login.microsoftonline.com/11111111-1111-1111-1111-111111111111\", resource=\"https://vault.azure.net\"")}}
            };
            Assert.Equal("unit-token",request.Headers.Authorization.Parameter);
            return new(HttpStatusCode.OK){Content=new StringContent(JsonSerializer.Serialize(new{
                value,id="https://tourlabazure-unit.vault.azure.net/secrets/"+name+"/"+version,attributes=new{enabled=true}
            }),Encoding.UTF8,"application/json")};
        })){Timeout=TimeSpan.FromSeconds(5)};
        var options=new SecretClientOptions{Transport=new HttpClientTransport(http)};options.Retry.MaxRetries=0;
        var reader=new VaultSecretReader(new SecretClient(new Uri("https://tourlabazure-unit.vault.azure.net/"),credential,options));
        Assert.Equal(value,await reader.ReadAsync(new Uri("https://tourlabazure-unit.vault.azure.net/"),name,version,default));
        Assert.All(calls,uri=>Assert.Equal("/secrets/"+name+"/"+version,uri.AbsolutePath));
        Assert.InRange(calls.Count,1,2);Assert.Contains("https://vault.azure.net/.default",credential.Scopes);
    }
    [Fact]
    public async Task DisabledOrExpiredSecretIsRejectedEvenWhenSdkReturnsSuccess()
    {
        foreach(var attributes in new object[]{new{enabled=false},new{enabled=true,exp=1}}){
            using var http=new HttpClient(new Handler((_,_)=>new(HttpStatusCode.OK){
                Content=new StringContent(JsonSerializer.Serialize(new{value="synthetic-test",id="https://tourlabazure-unit.vault.azure.net/secrets/key/"+new string('1',32),attributes}),Encoding.UTF8,"application/json")
            }));
            var options=new SecretClientOptions{Transport=new HttpClientTransport(http)};options.Retry.MaxRetries=0;
            var reader=new VaultSecretReader(new SecretClient(new Uri("https://tourlabazure-unit.vault.azure.net/"),new Credential(),options));
            Assert.Equal("InvalidSigningSecret",(await Assert.ThrowsAsync<SecurityConfigurationException>(()=>
                reader.ReadAsync(new Uri("https://tourlabazure-unit.vault.azure.net/"),"key",new string('1',32),default))).Message);
        }
    }
    private sealed class Handler(Func<HttpRequestMessage,CancellationToken,HttpResponseMessage> handler):HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct){ct.ThrowIfCancellationRequested();return Task.FromResult(handler(request,ct));}
    }
    private sealed class Credential:TokenCredential
    {
        public readonly List<string> Scopes=[];
        public override AccessToken GetToken(TokenRequestContext context,CancellationToken ct){Scopes.AddRange(context.Scopes);return new("unit-token",DateTimeOffset.UtcNow.AddHours(1));}
        public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext context,CancellationToken ct)=>ValueTask.FromResult(GetToken(context,ct));
    }
}