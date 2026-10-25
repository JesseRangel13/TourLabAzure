using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TourLab.Azure.Infrastructure.Security;
namespace TourLab.Azure.Tests;
public sealed class IngressSecurityTests
{
    [Theory][InlineData("10.10.0.4",true)][InlineData("203.0.113.50",false)]
    public async Task ForgedForwardedProtoOnlyChangesSchemeFromAnExplicitTrustedProxy(string remote,bool trusted)
    {
        var c=new ConfigurationManager();c.AddInMemoryCollection(new Dictionary<string,string?>{["Ingress:KnownProxies:0"]="10.10.0.4"});
        var context=new DefaultHttpContext();context.Request.Scheme="http";context.Request.Headers["X-Forwarded-Proto"]="https";
        context.Connection.RemoteIpAddress=System.Net.IPAddress.Parse(remote);
        var middleware=new ForwardedHeadersMiddleware(_=>Task.CompletedTask,NullLoggerFactory.Instance,Options.Create(CloudIngress.Options(c)));
        await middleware.Invoke(context);Assert.Equal(trusted,context.Request.IsHttps);
    }
    [Theory][InlineData("0.0.0.0")][InlineData("::")][InlineData("127.0.0.1")][InlineData("*")][InlineData("10.0.0.0/8")]
    public void UnspecifiedLoopbackAndBroadProxyTrustAreRejected(string address)
    {
        var c=new ConfigurationManager();c.AddInMemoryCollection(new Dictionary<string,string?>{["Ingress:KnownProxies:0"]=address});
        Assert.Throws<SecurityConfigurationException>(()=>CloudIngress.Options(c));
    }
}