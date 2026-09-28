using System.Diagnostics;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Themes.Fluent;
using RemoteDesktopClient.Core.Configuration;

namespace RemoteDesktopClient.UI.Avalonia;

/// <summary>The Avalonia application object. Styles are set up entirely in
/// code (no .axaml) to match the rest of this codebase's "build the UI in
/// code" convention.</summary>
public sealed class App : global::Avalonia.Application
{
    public static AppSettings Settings { get; set; } = null!;

    public static bool ScreenShareAvailable { get; set; } = true;

    public override void Initialize()
    {
        Styles.Add(new FluentTheme());
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow(Settings, ScreenShareAvailable);
        }

        base.OnFrameworkInitializationCompleted();
    }

    public static void Restart()
    {
        var exePath = Environment.ProcessPath;
        if (exePath is null)
        {
            Trace.WriteLine("App.Restart: could not determine the current executable path; not restarting.");
            return;
        }

        Process.Start(exePath);

        if (Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.Shutdown();
    }
}
