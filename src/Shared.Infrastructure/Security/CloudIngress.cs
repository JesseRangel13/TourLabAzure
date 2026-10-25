using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
namespace TourLab.Azure.Infrastructure.Security;

// Trust exact, reviewed proxy addresses only. No wildcard/network-wide trust or ambient override.
public static class CloudIngress
{
    public static ForwardedHeadersOptions Options(IConfiguration config)
    {
        var values=config.GetSection("Ingress:KnownProxies").Get<string[]>()??[];
        AzureSecurity.Require(values.Length is >=1 and <=8,"ExplicitTrustedProxiesRequired");
        var options=new ForwardedHeadersOptions {
            ForwardedHeaders=ForwardedHeaders.XForwardedProto,ForwardLimit=1,RequireHeaderSymmetry=true
        };
        options.KnownProxies.Clear();options.KnownIPNetworks.Clear();
        foreach(var value in values){
            AzureSecurity.Require(IPAddress.TryParse(value,out var address)&&
                !IPAddress.IsLoopback(address)&&!address.Equals(IPAddress.Any)&&!address.Equals(IPAddress.IPv6Any),"UnsafeTrustedProxy");
            options.KnownProxies.Add(IPAddress.Parse(value));
        }
        return options;
    }
}