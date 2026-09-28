using System.Diagnostics;
using System.Security.Cryptography;

namespace RemoteDesktopClient.Services.Update;

public sealed record ValidatedUpdate(string ExePath, string Sha256Hex);

public sealed class UpdateDownloader
{
    private const long MinValidExeBytes = 50 * 1024 * 1024;

    private readonly HttpClient _httpClient = new();

    public async Task<ValidatedUpdate?> DownloadAndValidateAsync(GitHubReleaseInfo release)
    {
        string? exeTempPath = null;
        try
        {
            var expectedHashText = await _httpClient.GetStringAsync(release.ChecksumDownloadUrl);
            var expectedHash = ExtractHexDigest(expectedHashText);
            if (expectedHash is null)
            {
                Trace.WriteLine("UpdateDownloader: checksum sidecar did not contain a recognizable SHA-256 hex digest.");
                return null;
            }

            var tempDir = Path.Combine(Path.GetTempPath(), "RemoteConnect-Update");
            Directory.CreateDirectory(tempDir);
            exeTempPath = Path.Combine(tempDir, $"RemoteConnect-{Guid.NewGuid():N}.exe");

            await using (var httpStream = await _httpClient.GetStreamAsync(release.ExeDownloadUrl))
            await using (var fileStream = File.Create(exeTempPath))
            {
                await httpStream.CopyToAsync(fileStream);
            }

            var downloadedLength = new FileInfo(exeTempPath).Length;
            if (downloadedLength < MinValidExeBytes)
            {
                Trace.WriteLine($"UpdateDownloader: downloaded exe is only {downloadedLength} bytes - too small to be a real self-contained build, discarding.");
                File.Delete(exeTempPath);
                return null;
            }

            var actualHash = await ComputeSha256Async(exeTempPath);
            if (!string.Equals(actualHash, expectedHash, StringComparison.OrdinalIgnoreCase))
            {
                Trace.WriteLine($"UpdateDownloader: downloaded exe's SHA-256 ({actualHash}) does not match the published checksum ({expectedHash}) - discarding, not trusting this file.");
                File.Delete(exeTempPath);
                return null;
            }

            return new ValidatedUpdate(exeTempPath, actualHash);
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"UpdateDownloader: failed to download/validate update. {ex}");
            if (exeTempPath is not null)
            {
                try { File.Delete(exeTempPath); } catch { }
            }
            return null;
        }
    }

    internal static string? ExtractHexDigest(string text)
    {
        var token = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        return token is { Length: 64 } && token.All(Uri.IsHexDigit)
            ? token.ToLowerInvariant()
            : null;
    }

    internal static async Task<string> ComputeSha256Async(string filePath)
    {
        await using var stream = File.OpenRead(filePath);
        var hash = await SHA256.HashDataAsync(stream);
        return Convert.ToHexStringLower(hash);
    }
}
