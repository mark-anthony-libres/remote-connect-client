using System.Diagnostics;
using System.Reflection;
using Avalonia.Threading;
using RemoteDesktopClient.Services.Update;

namespace RemoteDesktopClient.UI.Avalonia;

public sealed partial class MainWindow
{
    private static readonly GitHubUpdateClient UpdateClient = new();
    private static readonly UpdateDownloader Downloader = new();

    private bool IsSessionActive =>
        _remoteSessionWindow is not null
        || _activeToolbarRequestId is not null
        || _requesterConnectingRequestId is not null
        || _webRtcRequestId is not null;

    private async Task CheckForUpdateAsync()
    {
        var repo = _settings.UpdateGitHubRepo;
        if (string.IsNullOrWhiteSpace(repo))
            return;

        var release = await UpdateClient.GetLatestReleaseAsync(repo);
        if (release is null)
            return;

        var currentVersion = Assembly.GetExecutingAssembly().GetName().Version;
        if (currentVersion is null || release.Version <= currentVersion)
            return;

        while (IsSessionActive)
            await Task.Delay(TimeSpan.FromSeconds(30));

        var newVersionText = release.Version.ToString();
        Dispatcher.UIThread.Post(() => ShowUpdateAvailableModal(newVersionText, () => _ = OnUpdateRequestedAsync(release)));
    }

    private async Task OnUpdateRequestedAsync(GitHubReleaseInfo release)
    {
        ShowLoadingModal("Updating", "Downloading the new version...");

        var validated = await Downloader.DownloadAndValidateAsync(release);
        if (validated is null)
        {
            HideModal();
            ShowMessageModal("Update Failed", "Could not download or verify the update. Please try again later.");
            return;
        }

        while (IsSessionActive)
            await Task.Delay(TimeSpan.FromSeconds(30));

        if (!TryLaunchUpdateHelper(validated))
        {
            HideModal();
            ShowMessageModal("Update Failed", "Could not start the updater. Please try again later.");
            return;
        }

        Close();
    }

    private static bool TryLaunchUpdateHelper(ValidatedUpdate validated)
    {
        try
        {
            var assembly = Assembly.GetExecutingAssembly();
            using var resourceStream = assembly.GetManifestResourceStream("RemoteConnect.Updater.exe");
            if (resourceStream is null)
            {
                Trace.WriteLine("MainWindow: update helper is not embedded in this build - cannot install the update (this build was likely not produced by publish.ps1).");
                return false;
            }

            var helperDirectory = Path.Combine(Path.GetTempPath(), "RemoteConnect-Update");
            Directory.CreateDirectory(helperDirectory);
            var helperPath = Path.Combine(helperDirectory, $"RemoteConnect.Updater-{Guid.NewGuid():N}.exe");
            using (var fileStream = File.Create(helperPath))
                resourceStream.CopyTo(fileStream);

            var currentExePath = Environment.ProcessPath
                ?? throw new InvalidOperationException("Environment.ProcessPath was null - cannot determine this exe's own path to update.");

            Process.Start(new ProcessStartInfo(helperPath)
            {
                Arguments = $"\"{currentExePath}\" \"{validated.ExePath}\" {Environment.ProcessId} {validated.Sha256Hex}",
                UseShellExecute = false,
                CreateNoWindow = true,
            });
            return true;
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"MainWindow: failed to launch the update helper. {ex}");
            return false;
        }
    }
}
