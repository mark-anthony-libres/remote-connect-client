using RemoteDesktopClient.Services.Update;
using Xunit;

namespace RemoteDesktopClient.Tests.Services;

public class GitHubUpdateClientTests
{
    private const string ValidReleaseJson = """
        {
          "tag_name": "v1.2.0",
          "name": "v1.2.0",
          "assets": [
            {
              "name": "RemoteConnect.exe",
              "browser_download_url": "https://github.com/mark-anthony-libres/remote-connect-client/releases/download/v1.2.0/RemoteConnect.exe"
            },
            {
              "name": "RemoteConnect.exe.sha256",
              "browser_download_url": "https://github.com/mark-anthony-libres/remote-connect-client/releases/download/v1.2.0/RemoteConnect.exe.sha256"
            }
          ]
        }
        """;

    [Fact]
    public void ParseLatestRelease_ExtractsVersionAndBothAssetUrls()
    {
        var release = GitHubUpdateClient.ParseLatestRelease(ValidReleaseJson);

        Assert.NotNull(release);
        Assert.Equal(new Version(1, 2, 0), release!.Version);
        Assert.Equal("https://github.com/mark-anthony-libres/remote-connect-client/releases/download/v1.2.0/RemoteConnect.exe", release.ExeDownloadUrl);
        Assert.Equal("https://github.com/mark-anthony-libres/remote-connect-client/releases/download/v1.2.0/RemoteConnect.exe.sha256", release.ChecksumDownloadUrl);
    }

    [Fact]
    public void ParseLatestRelease_NoAssetsAtAll_ReturnsNull()
    {
        var release = GitHubUpdateClient.ParseLatestRelease("""{"tag_name":"v2.0.0","assets":[]}""");

        Assert.Null(release);
    }

    [Fact]
    public void ParseLatestRelease_WithoutLeadingV_StillParses()
    {
        var release = GitHubUpdateClient.ParseLatestRelease("""
            {
              "tag_name": "1.0.5",
              "assets": [
                {"name": "RemoteConnect.exe", "browser_download_url": "https://example.com/RemoteConnect.exe"},
                {"name": "RemoteConnect.exe.sha256", "browser_download_url": "https://example.com/RemoteConnect.exe.sha256"}
              ]
            }
            """);

        Assert.NotNull(release);
        Assert.Equal(new Version(1, 0, 5), release!.Version);
    }

    [Fact]
    public void ParseLatestRelease_MissingChecksumAsset_ReturnsNull()
    {
        var release = GitHubUpdateClient.ParseLatestRelease("""
            {
              "tag_name": "v1.0.0",
              "assets": [
                {"name": "RemoteConnect.exe", "browser_download_url": "https://example.com/RemoteConnect.exe"}
              ]
            }
            """);

        Assert.Null(release);
    }

    [Fact]
    public void ParseLatestRelease_UnparsableTag_ReturnsNull()
    {
        var release = GitHubUpdateClient.ParseLatestRelease("""{"tag_name":"not-a-version","assets":[]}""");

        Assert.Null(release);
    }

    [Fact]
    public void ParseLatestRelease_MissingTagName_ReturnsNull()
    {
        var release = GitHubUpdateClient.ParseLatestRelease("""{"assets":[]}""");

        Assert.Null(release);
    }

    [Fact]
    public void ParseLatestRelease_NonObjectJson_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => GitHubUpdateClient.ParseLatestRelease("[]"));
    }
}
