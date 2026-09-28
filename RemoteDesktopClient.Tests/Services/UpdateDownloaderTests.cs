using RemoteDesktopClient.Services.Update;
using Xunit;

namespace RemoteDesktopClient.Tests.Services;

public class UpdateDownloaderTests
{
    private static readonly string SampleHash = string.Concat(Enumerable.Repeat("0123456789abcdef", 4));

    [Fact]
    public void ExtractHexDigest_BareDigest_ReturnsIt()
    {
        Assert.Equal(SampleHash, UpdateDownloader.ExtractHexDigest(SampleHash));
    }

    [Fact]
    public void ExtractHexDigest_ShaSumStyleWithFilename_TakesFirstToken()
    {
        Assert.Equal(SampleHash, UpdateDownloader.ExtractHexDigest($"{SampleHash}  RemoteConnect.exe\n"));
    }

    [Fact]
    public void ExtractHexDigest_MixedCase_IsNormalizedToLowercase()
    {
        Assert.Equal(SampleHash, UpdateDownloader.ExtractHexDigest(SampleHash.ToUpperInvariant()));
    }

    [Fact]
    public void ExtractHexDigest_TooShort_ReturnsNull()
    {
        Assert.Null(UpdateDownloader.ExtractHexDigest("abc123"));
    }

    [Fact]
    public void ExtractHexDigest_NotHex_ReturnsNull()
    {
        Assert.Null(UpdateDownloader.ExtractHexDigest(new string('z', 64)));
    }

    [Fact]
    public void ExtractHexDigest_Empty_ReturnsNull()
    {
        Assert.Null(UpdateDownloader.ExtractHexDigest(""));
        Assert.Null(UpdateDownloader.ExtractHexDigest("   \n  "));
    }

    [Fact]
    public async Task ComputeSha256Async_MatchesKnownDigestForKnownContent()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(tempFile, "hello world");

            var hash = await UpdateDownloader.ComputeSha256Async(tempFile);

            Assert.Equal("b94d27b9934d3e08a52e52d7da7dabfac484efe37a5380ee9088f7ace2efcde9", hash);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }
}
