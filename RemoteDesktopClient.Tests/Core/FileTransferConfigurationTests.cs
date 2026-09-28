using RemoteDesktopClient.Core.Configuration;
using RemoteDesktopClient.Core.Control;

namespace RemoteDesktopClient.Tests.Core;

public sealed class FileTransferConfigurationTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-20)]
    public void AppSettings_RejectsNonPositiveFileTransferMaxTurnMb(int value)
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => CreateSettings(value));
        Assert.Contains("FILE_TRANSFER_MAX_TURN_MB must be positive", ex.Message);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(20)]
    [InlineData(500)]
    public void AppSettings_AcceptsPositiveFileTransferMaxTurnMb(int value)
    {
        Assert.Equal(value, CreateSettings(value).FileTransferMaxTurnMb);
    }

    [Fact]
    public void FileTransferMaxTurnMb_IsBoundToTheFileTransferMaxTurnMbEnvKey()
    {
        var property = typeof(AppSettings).GetProperty(nameof(AppSettings.FileTransferMaxTurnMb))!;
        var envKey = (EnvKeyAttribute)Attribute.GetCustomAttribute(property, typeof(EnvKeyAttribute))!;
        Assert.Equal("FILE_TRANSFER_MAX_TURN_MB", envKey.Key);
    }

    [Fact]
    public void Factory_ReadsFileTransferMaxTurnMb_FromEnvironment()
    {
        var previous = Environment.GetEnvironmentVariable("FILE_TRANSFER_MAX_TURN_MB");
        try
        {
            Environment.SetEnvironmentVariable("FILE_TRANSFER_MAX_TURN_MB", "37");
            Assert.Equal(37, AppSettingsFactory.Load().FileTransferMaxTurnMb);

            Environment.SetEnvironmentVariable("FILE_TRANSFER_MAX_TURN_MB", "0");
            Assert.Throws<ArgumentOutOfRangeException>(() => AppSettingsFactory.Load());
        }
        finally
        {
            Environment.SetEnvironmentVariable("FILE_TRANSFER_MAX_TURN_MB", previous);
        }
    }

    [Fact]
    public void Limit_IsInBinaryMegabytes_AndInclusive()
    {
        Assert.Equal(20L * 1024 * 1024, FileTransferPolicy.MaxRelayedFileBytes(20));
        Assert.True(FileTransferPolicy.IsAllowed(20L * 1024 * 1024, isRelayed: true, maxTurnMb: 20));
        Assert.False(FileTransferPolicy.IsAllowed(20L * 1024 * 1024 + 1, isRelayed: true, maxTurnMb: 20));
        Assert.True(FileTransferPolicy.IsAllowed(long.MaxValue, isRelayed: false, maxTurnMb: 20));
    }

    [Fact]
    public void ClipboardFileTransfer_RejectsNonPositiveLimit()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ClipboardFileTransfer(
            isTarget: true, maxTurnMb: 0, isRelayed: () => true, send: _ => { }, bufferedAmount: () => 0, stagingRoot: Path.GetTempPath()));
    }

    private static AppSettings CreateSettings(int fileTransferMaxTurnMb) => new()
    {
        DeviceWebSocketUrl = "ws://localhost:8000/api/ws/device",
        StunServerUrl = null,
        TurnServerUrl = null,
        TurnUsername = null,
        TurnCredential = null,
        MaxVideoBitrateKbps = 1000,
        ScreenCaptureFps = 60,
        MaxScreenCaptureFps = 15,
        ClipboardImageMaxBitrateKbps = 15,
        FileTransferMaxTurnMb = fileTransferMaxTurnMb,
        ShowRemoteDebug = false,
        TelemetrySampleIntervalSeconds = 1,
        EnableSessionThumbnails = true,
        ClientLogMaxFileSizeMb = 10,
        ClientLogMaxFiles = 10,
        UpdateGitHubRepo = null,
    };
}
