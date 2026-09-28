using System.Diagnostics;
using Avalonia.Media.Imaging;

namespace RemoteDesktopClient.UI.Avalonia.Components.Shared;

internal static class DeviceThumbnailCache
{
    private static readonly string CacheDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "RemoteConnect", "thumbnails");

    public static void Save(string deviceId, Bitmap frame)
    {
        try
        {
            Directory.CreateDirectory(CacheDirectory);
            using var stream = File.Create(PathFor(deviceId));
            frame.Save(stream, PngBitmapEncoderOptions.Default);
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"DeviceThumbnailCache: failed to save thumbnail for {deviceId}. {ex}");
        }
    }

    public static Bitmap? TryLoad(string deviceId)
    {
        try
        {
            var path = PathFor(deviceId);
            return File.Exists(path) ? new Bitmap(path) : null;
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"DeviceThumbnailCache: failed to load thumbnail for {deviceId}. {ex}");
            return null;
        }
    }

    private static string PathFor(string deviceId)
    {
        var sanitized = new string(deviceId.Where(char.IsLetterOrDigit).ToArray());
        var safeName = string.IsNullOrEmpty(sanitized) ? "unknown" : sanitized;
        return Path.Combine(CacheDirectory, $"{safeName}.png");
    }
}
