using RemoteDesktopClient.Services;

namespace RemoteDesktopClient.Tests.Services;

public class DeviceIdentityClientTests
{
    [Fact]
    public void BuildRecentConnectionsUri_StripsWsDeviceSuffix_AndSwapsSchemeToHttp()
    {
        var client = new DeviceIdentityClient("ws://localhost:8000/api/ws/device");

        var uri = client.BuildRecentConnectionsUri();

        Assert.Equal("http://localhost:8000/api/devices/recent-connections", uri.ToString());
    }

    [Fact]
    public void BuildRecentConnectionsUri_SwapsWssToHttps()
    {
        var client = new DeviceIdentityClient("wss://backend.example.com/api/ws/device");

        var uri = client.BuildRecentConnectionsUri();

        Assert.Equal("https://backend.example.com/api/devices/recent-connections", uri.ToString());
    }

    [Fact]
    public void BuildRecentConnectionsUri_PreservesAnExplicitPort()
    {
        var client = new DeviceIdentityClient("ws://172.22.112.1:8000/api/ws/device");

        var uri = client.BuildRecentConnectionsUri();

        Assert.Equal(8000, uri.Port);
        Assert.Equal("172.22.112.1", uri.Host);
    }

    [Fact]
    public void BuildRecentConnectionsUri_DoesNotCrash_WhenWsUrlLacksTheExpectedSuffix()
    {
        var client = new DeviceIdentityClient("ws://localhost:8000/something-else");

        var uri = client.BuildRecentConnectionsUri();

        Assert.Equal("http://localhost:8000/something-else/devices/recent-connections", uri.ToString());
    }
}
