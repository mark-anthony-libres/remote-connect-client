namespace RemoteDesktopClient.WebRTC;

public static class VideoBitrateCalculator
{
    private static readonly (long Pixels, int Kbps)[] CalibrationPoints =
    [
        (1280L * 720, 1500),
        (1920L * 1080, 3000),
        (2560L * 1440, 5000),
        (3840L * 2160, 8000),
    ];

    private const int CalibrationFrameRate = 15;

    public static int CalculateKbps(int width, int height, int frameRate)
    {
        var pixels = (long)width * height;
        var kbpsAt15Fps = InterpolateKbps(pixels);
        return frameRate == CalibrationFrameRate
            ? kbpsAt15Fps
            : Math.Max(1, (int)Math.Round(kbpsAt15Fps * (frameRate / (double)CalibrationFrameRate)));
    }

    private static int InterpolateKbps(long pixels)
    {
        var segmentIndex = Math.Clamp(
            Array.FindLastIndex(CalibrationPoints, point => point.Pixels <= pixels),
            0, CalibrationPoints.Length - 2);
        var (p1, k1) = CalibrationPoints[segmentIndex];
        var (p2, k2) = CalibrationPoints[segmentIndex + 1];

        var t = (double)(pixels - p1) / (p2 - p1);
        return Math.Max(1, (int)Math.Round(k1 + t * (k2 - k1)));
    }

    public static int ApplyMaxBitrate(int baseBitrateKbps, int maxBitrateKbps) =>
        Math.Min(baseBitrateKbps, maxBitrateKbps);
}
