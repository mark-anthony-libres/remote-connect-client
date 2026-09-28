using System.Diagnostics;
using System.Security.Cryptography;

if (args.Length != 4)
{
    return 1;
}

var oldExePath = args[0];
var newExePath = args[1];
var expectedSha256 = args[3];

if (!int.TryParse(args[2], out var parentPid))
{
    return 1;
}

Log($"=== Update helper started: old={oldExePath} new={newExePath} parentPid={parentPid} ===");

try
{
    using var parent = Process.GetProcessById(parentPid);
    if (!parent.WaitForExit(30_000))
    {
        Log($"Parent process {parentPid} did not exit within 30s - aborting, nothing was touched.");
        return 1;
    }
}
catch (ArgumentException)
{
}

var unlocked = false;
for (var attempt = 1; attempt <= 5 && !unlocked; attempt++)
{
    try
    {
        using var handle = File.Open(oldExePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        unlocked = true;
    }
    catch (IOException)
    {
        Thread.Sleep(1000);
    }
}

if (!unlocked)
{
    Log($"{oldExePath} is still locked after retries - aborting, nothing was touched.");
    return 1;
}

var backupPath = oldExePath + ".old";
try
{
    if (File.Exists(backupPath))
        File.Delete(backupPath);
    File.Move(oldExePath, backupPath);

    File.Move(newExePath, oldExePath);

    var actualHash = ComputeSha256(oldExePath);
    if (!string.Equals(actualHash, expectedSha256, StringComparison.OrdinalIgnoreCase))
        throw new InvalidOperationException($"post-move SHA-256 mismatch: expected {expectedSha256}, got {actualHash}.");

    File.Delete(backupPath);
    Log("Update applied successfully - starting new version.");
    StartExe(oldExePath);
    return 0;
}
catch (Exception ex)
{
    Log($"Update failed - rolling back: {ex}");
    try
    {
        if (File.Exists(backupPath))
        {
            if (File.Exists(oldExePath))
                File.Delete(oldExePath);
            File.Move(backupPath, oldExePath);
        }

        if (File.Exists(oldExePath))
        {
            Log("Rolled back to the previous version - relaunching it.");
            StartExe(oldExePath);
        }
        else
        {
            Log("Rollback could not restore the previous version - no exe left to relaunch.");
        }
    }
    catch (Exception rollbackEx)
    {
        Log($"Rollback ALSO failed - the app may need to be reinstalled: {rollbackEx}");
    }

    return 1;
}

static void StartExe(string path) => Process.Start(new ProcessStartInfo(path)
{
    UseShellExecute = true,
    WorkingDirectory = Path.GetDirectoryName(path) ?? string.Empty,
});

static string ComputeSha256(string filePath)
{
    using var stream = File.OpenRead(filePath);
    var hash = SHA256.HashData(stream);
    return Convert.ToHexStringLower(hash);
}

static void Log(string message)
{
    try
    {
        var logPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "RemoteConnect", "logs", "update-helper.log");
        Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
        File.AppendAllText(logPath, $"{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz} {message}{Environment.NewLine}");
    }
    catch
    {
    }
}
