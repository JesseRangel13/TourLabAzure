using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using TourLab.Azure.Infrastructure.Messaging;

namespace TourLab.Azure.Tests;
public sealed class IsolatedBrokerConfigurationTests
{
    private const string Suffix=";SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;";
    private sealed class EnvironmentProfile(string name):IHostEnvironment
    {
        public string EnvironmentName{get;set;}=name;
        public string ApplicationName{get;set;}="IsolatedBrokerTests";
        public string ContentRootPath{get;set;}="";
        public IFileProvider ContentRootFileProvider{get;set;}=new NullFileProvider();
    }
    [Theory]
    [InlineData(5673,false,true)]
    [InlineData(5674,true,true)]
    [InlineData(5674,false,false)]
    [InlineData(5673,true,false)]
    [InlineData(5672,false,false)]
    [InlineData(5675,true,false)]
    public async Task SeparateTestPortRequiresExplicitDevelopmentOptIn(int port,bool isolated,bool accepted)
    {
        var connection="Endpoint=sb://localhost:"+port+Suffix;
        Assert.Equal(accepted,LocalBrokerEndpoint.Safe(connection,true,isolated));
        Assert.False(LocalBrokerEndpoint.Safe(connection,false,isolated));
        var options=Options.Create(new MessagingOptions{Enabled=true,ConnectionString=connection,LocalTestBroker=isolated});
        if(accepted){await using var transport=new ServiceBusTransport(options,new EnvironmentProfile("Development"));}
        else Assert.Throws<InvalidOperationException>(()=>new ServiceBusTransport(options,new EnvironmentProfile("Development")));
    }
    [Fact]
    public void TestFlagCannotPermitExternalHostsOrCloudFallback()
    {
        var connection="Endpoint=sb://example.org:5674"+Suffix;
        Assert.False(LocalBrokerEndpoint.Safe(connection,true,true));
        Assert.Throws<InvalidOperationException>(()=>new ServiceBusTransport(Options.Create(new MessagingOptions{
            Enabled=true,ConnectionString=connection,LocalTestBroker=true}),new EnvironmentProfile("Development")));
        Assert.Throws<TourLab.Azure.Infrastructure.Security.SecurityConfigurationException>(()=>new ServiceBusTransport(Options.Create(new MessagingOptions{
            Enabled=true,FullyQualifiedNamespace="tourlabazure-test.servicebus.windows.net",LocalTestBroker=true}),new EnvironmentProfile("Production")));
    }
}
