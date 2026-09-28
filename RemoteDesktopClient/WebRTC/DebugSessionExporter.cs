using System.Diagnostics;

namespace RemoteDesktopClient.WebRTC;

internal static class DebugSessionExporter
{
    public static string BuildFolderPath(string requestId) => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "RemoteConnect", "logs", "debug-session", RemoteSessionTelemetry.SanitizeRequestId(requestId));

    public static void Export(string requestId, string ownLogFilePath, byte[]? targetLogBytes)
    {
        try
        {
            var folder = BuildFolderPath(requestId);
            Directory.CreateDirectory(folder);

            if (File.Exists(ownLogFilePath))
                File.Copy(ownLogFilePath, Path.Combine(folder, "requester.log"), overwrite: true);
            else
                Trace.WriteLine($"DebugSessionExporter: no local session log found for {requestId} - requester.log not created.");

            if (targetLogBytes is not null)
                File.WriteAllBytes(Path.Combine(folder, "target.log"), targetLogBytes);
            else
                Trace.WriteLine($"DebugSessionExporter: target debug log unavailable for {requestId} (target's SHOW_REMOTE_DEBUG was off, or the transfer didn't complete) - target.log not created.");
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"DebugSessionExporter: failed to export debug-session bundle for {requestId}. {ex}");
        }
    }
}
