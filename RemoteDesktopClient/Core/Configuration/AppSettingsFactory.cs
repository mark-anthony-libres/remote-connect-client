namespace RemoteDesktopClient.Core.Configuration;

internal static class AppSettingsFactory
{
    public static AppSettings Load()
    {
        EnvFile.Load();

        return new AppSettings
        {
            DeviceWebSocketUrl = Environment.GetEnvironmentVariable("DEVICE_WS_URL")
                ?? "ws://localhost:8000/api/ws/device",

            StunServerUrl = NullIfBlank(Environment.GetEnvironmentVariable("STUN_SERVER_URL")),
            TurnServerUrl = NullIfBlank(Environment.GetEnvironmentVariable("TURN_SERVER_URL")),
            TurnUsername = NullIfBlank(Environment.GetEnvironmentVariable("TURN_USERNAME")),
            TurnCredential = NullIfBlank(Environment.GetEnvironmentVariable("TURN_CREDENTIAL")),

            MaxVideoBitrateKbps = int.TryParse(Environment.GetEnvironmentVariable("MAX_VIDEO_BITRATE_KBPS"), out var maxVideoBitrateKbps)
                ? maxVideoBitrateKbps
                : 1000,

            ScreenCaptureFps = int.TryParse(Environment.GetEnvironmentVariable("SCREEN_CAPTURE_FPS"), out var screenCaptureFps)
                ? screenCaptureFps
                : 60,
            MaxScreenCaptureFps = int.TryParse(Environment.GetEnvironmentVariable("MAX_SCREEN_CAPTURE_FPS"), out var maxScreenCaptureFps)
                ? maxScreenCaptureFps
                : 15,

            ClipboardImageMaxBitrateKbps = int.TryParse(Environment.GetEnvironmentVariable("CLIPBOARD_IMAGE_MAX_BITRATE_KBPS"), out var clipboardImageMaxBitrateKbps)
                ? clipboardImageMaxBitrateKbps
                : 15,

            FileTransferMaxTurnMb = int.TryParse(Environment.GetEnvironmentVariable("FILE_TRANSFER_MAX_TURN_MB"), out var fileTransferMaxTurnMb)
                ? fileTransferMaxTurnMb
                : 20,

            ShowRemoteDebug = bool.TryParse(Environment.GetEnvironmentVariable("SHOW_REMOTE_DEBUG"), out var showRemoteDebug) && showRemoteDebug,

            TelemetrySampleIntervalSeconds = int.TryParse(Environment.GetEnvironmentVariable("TELEMETRY_SAMPLE_INTERVAL_SECONDS"), out var telemetrySampleIntervalSeconds)
                ? telemetrySampleIntervalSeconds
                : 1,

            EnableSessionThumbnails = !bool.TryParse(Environment.GetEnvironmentVariable("ENABLE_SESSION_THUMBNAILS"), out var enableSessionThumbnails) || enableSessionThumbnails,

            ClientLogMaxFileSizeMb = int.TryParse(Environment.GetEnvironmentVariable("CLIENT_LOG_MAX_FILE_SIZE_MB"), out var clientLogMaxFileSizeMb)
                ? clientLogMaxFileSizeMb
                : 10,
            ClientLogMaxFiles = int.TryParse(Environment.GetEnvironmentVariable("CLIENT_LOG_MAX_FILES"), out var clientLogMaxFiles)
                ? clientLogMaxFiles
                : 10,

            UpdateGitHubRepo = NullIfBlank(Environment.GetEnvironmentVariable("UPDATE_GITHUB_REPO")),
        };
    }

    private static string? NullIfBlank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;
}
