namespace RemoteDesktopClient.Core.Configuration;

public sealed class AppSettings
{
    [EnvKey("DEVICE_WS_URL")]
    public required string DeviceWebSocketUrl { get; init; }

    [EnvKey("STUN_SERVER_URL")]
    public required string? StunServerUrl { get; init; }

    [EnvKey("TURN_SERVER_URL")]
    public required string? TurnServerUrl { get; init; }

    [EnvKey("TURN_USERNAME")]
    public required string? TurnUsername { get; init; }

    [EnvKey("TURN_CREDENTIAL")]
    public required string? TurnCredential { get; init; }

    private readonly int _maxVideoBitrateKbps;
    [EnvKey("MAX_VIDEO_BITRATE_KBPS")]
    public required int MaxVideoBitrateKbps
    {
        get => _maxVideoBitrateKbps;
        init => _maxVideoBitrateKbps = value > 0
            ? value
            : throw new ArgumentOutOfRangeException(nameof(MaxVideoBitrateKbps), value, "MAX_VIDEO_BITRATE_KBPS must be positive.");
    }

    private readonly int _screenCaptureFps;
    [EnvKey("SCREEN_CAPTURE_FPS")]
    public required int ScreenCaptureFps
    {
        get => _screenCaptureFps;
        init => _screenCaptureFps = value > 0
            ? value
            : throw new ArgumentOutOfRangeException(nameof(ScreenCaptureFps), value, "SCREEN_CAPTURE_FPS must be positive.");
    }

    private readonly int _maxScreenCaptureFps;
    [EnvKey("MAX_SCREEN_CAPTURE_FPS")]
    public required int MaxScreenCaptureFps
    {
        get => _maxScreenCaptureFps;
        init => _maxScreenCaptureFps = value > 0
            ? value
            : throw new ArgumentOutOfRangeException(nameof(MaxScreenCaptureFps), value, "MAX_SCREEN_CAPTURE_FPS must be positive.");
    }

    private readonly int _clipboardImageMaxBitrateKbps;
    [EnvKey("CLIPBOARD_IMAGE_MAX_BITRATE_KBPS")]
    public required int ClipboardImageMaxBitrateKbps
    {
        get => _clipboardImageMaxBitrateKbps;
        init => _clipboardImageMaxBitrateKbps = value > 0
            ? value
            : throw new ArgumentOutOfRangeException(nameof(ClipboardImageMaxBitrateKbps), value, "CLIPBOARD_IMAGE_MAX_BITRATE_KBPS must be positive.");
    }

    private readonly int _fileTransferMaxTurnMb;
    [EnvKey("FILE_TRANSFER_MAX_TURN_MB")]
    public required int FileTransferMaxTurnMb
    {
        get => _fileTransferMaxTurnMb;
        init => _fileTransferMaxTurnMb = value > 0
            ? value
            : throw new ArgumentOutOfRangeException(nameof(FileTransferMaxTurnMb), value, "FILE_TRANSFER_MAX_TURN_MB must be positive.");
    }

    [EnvKey("SHOW_REMOTE_DEBUG")]
    public required bool ShowRemoteDebug { get; init; }

    private readonly int _telemetrySampleIntervalSeconds;
    [EnvKey("TELEMETRY_SAMPLE_INTERVAL_SECONDS")]
    public required int TelemetrySampleIntervalSeconds
    {
        get => _telemetrySampleIntervalSeconds;
        init => _telemetrySampleIntervalSeconds = value > 0
            ? value
            : throw new ArgumentOutOfRangeException(nameof(TelemetrySampleIntervalSeconds), value, "TELEMETRY_SAMPLE_INTERVAL_SECONDS must be positive.");
    }

    [EnvKey("ENABLE_SESSION_THUMBNAILS")]
    public required bool EnableSessionThumbnails { get; init; }

    private readonly int _clientLogMaxFileSizeMb;
    [EnvKey("CLIENT_LOG_MAX_FILE_SIZE_MB")]
    public required int ClientLogMaxFileSizeMb
    {
        get => _clientLogMaxFileSizeMb;
        init => _clientLogMaxFileSizeMb = value > 0
            ? value
            : throw new ArgumentOutOfRangeException(nameof(ClientLogMaxFileSizeMb), value, "CLIENT_LOG_MAX_FILE_SIZE_MB must be positive.");
    }

    private readonly int _clientLogMaxFiles;
    [EnvKey("CLIENT_LOG_MAX_FILES")]
    public required int ClientLogMaxFiles
    {
        get => _clientLogMaxFiles;
        init => _clientLogMaxFiles = value > 0
            ? value
            : throw new ArgumentOutOfRangeException(nameof(ClientLogMaxFiles), value, "CLIENT_LOG_MAX_FILES must be positive.");
    }

    [EnvKey("UPDATE_GITHUB_REPO")]
    public required string? UpdateGitHubRepo { get; init; }
}
