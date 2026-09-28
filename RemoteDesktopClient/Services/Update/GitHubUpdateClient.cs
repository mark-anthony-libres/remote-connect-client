using System.Diagnostics;
using System.Text.Json;

namespace RemoteDesktopClient.Services.Update;

public sealed record GitHubReleaseInfo(Version Version, string ExeDownloadUrl, string ChecksumDownloadUrl);

public sealed class GitHubUpdateClient
{
    private readonly HttpClient _httpClient = new();

    public async Task<GitHubReleaseInfo?> GetLatestReleaseAsync(string repo)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"https://api.github.com/repos/{repo}/releases/latest");
            request.Headers.UserAgent.ParseAdd("RemoteConnect-Updater");
            request.Headers.Accept.ParseAdd("application/vnd.github+json");

            using var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                Trace.WriteLine($"GitHubUpdateClient: GET releases/latest for {repo} returned HTTP {(int)response.StatusCode}.");
                return null;
            }

            var json = await response.Content.ReadAsStringAsync();
            var release = ParseLatestRelease(json);
            if (release is null)
                Trace.WriteLine($"GitHubUpdateClient: {repo}'s latest release is missing a parsable version or one of the expected assets (RemoteConnect.exe / RemoteConnect.exe.sha256).");
            return release;
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"GitHubUpdateClient: failed to check {repo} for updates. {ex}");
            return null;
        }
    }

    internal static GitHubReleaseInfo? ParseLatestRelease(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        if (!root.TryGetProperty("tag_name", out var tagProperty))
            return null;
        var tag = tagProperty.GetString();
        if (string.IsNullOrWhiteSpace(tag))
            return null;

        var versionText = tag.StartsWith('v') || tag.StartsWith('V') ? tag[1..] : tag;
        if (!Version.TryParse(versionText, out var version))
            return null;

        string? exeUrl = null;
        string? checksumUrl = null;
        if (root.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Array)
        {
            foreach (var asset in assets.EnumerateArray())
            {
                var name = asset.TryGetProperty("name", out var nameProperty) ? nameProperty.GetString() : null;
                var url = asset.TryGetProperty("browser_download_url", out var urlProperty) ? urlProperty.GetString() : null;
                if (name is null || url is null)
                    continue;

                if (string.Equals(name, "RemoteConnect.exe", StringComparison.OrdinalIgnoreCase))
                    exeUrl = url;
                else if (string.Equals(name, "RemoteConnect.exe.sha256", StringComparison.OrdinalIgnoreCase))
                    checksumUrl = url;
            }
        }

        return exeUrl is not null && checksumUrl is not null
            ? new GitHubReleaseInfo(version, exeUrl, checksumUrl)
            : null;
    }
}
