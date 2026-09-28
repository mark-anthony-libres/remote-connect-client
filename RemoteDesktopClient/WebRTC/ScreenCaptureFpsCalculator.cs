namespace RemoteDesktopClient.WebRTC;

public static class ScreenCaptureFpsCalculator
{
    public static int Calculate(int configuredFps, bool isRelayed, int maxFpsWhenRelayed) =>
        isRelayed ? Math.Min(configuredFps, maxFpsWhenRelayed) : configuredFps;
}
