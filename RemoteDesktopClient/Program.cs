using System.Diagnostics;
using Avalonia;
using RemoteDesktopClient.Core;
using RemoteDesktopClient.Core.Configuration;
using RemoteDesktopClient.UI.Avalonia;
using SIPSorceryMedia.FFmpeg;

namespace RemoteDesktopClient;

static class Program
{
    /// <summary>
    ///  The main entry point for the application.
    /// </summary>
    [STAThread]
    static void Main(string[] args)
    {
        if (!SingleInstance.TryAcquire())
            return;

        App.Settings = AppSettingsFactory.Load();
        InitialiseFileLogging();
        InitialiseFFmpeg();
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    private static void InitialiseFileLogging()
    {
        try
        {
            var logDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "RemoteConnect", "logs");
            var settings = App.Settings;
            Trace.Listeners.Add(new RotatingFileTraceListener(logDirectory, settings.ClientLogMaxFileSizeMb, settings.ClientLogMaxFiles));
            Trace.AutoFlush = true;
            Trace.WriteLine($"=== RemoteDesktopClient started {DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz} ===");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Program: file logging setup failed - continuing without it. {ex}");
        }
    }

    private static void InitialiseFFmpeg()
    {
        try
        {
            var bundledFfmpegPath = Path.Combine(AppContext.BaseDirectory, "ffmpeg");
            FFmpegInit.Initialise(libPath: bundledFfmpegPath);
        }
        catch (Exception ex)
        {
            App.ScreenShareAvailable = false;
            Trace.WriteLine($"Program: bundled FFmpeg initialisation failed - the remote-session video feature will not work this run. {ex}");
        }
    }

    private static AppBuilder BuildAvaloniaApp()
    {
        var builder = AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace();

        if (OperatingSystem.IsWindows())
        {
            builder = ConfigureWindowsRendering(builder);
        }

        return builder;
    }

    private static AppBuilder ConfigureWindowsRendering(AppBuilder builder)
    {
        var interactive = Environment.UserInteractive;
        Trace.WriteLine($"Program: session type = {(interactive ? "interactive" : "non-interactive")} (Environment.UserInteractive={interactive}).");

        if (!interactive)
        {
            Trace.WriteLine("Program: forcing RenderingMode=[Software], CompositionMode=[RedirectionSurface] - no interactive window station, so WinUIComposition/DirectComposition/AngleEgl would fail or crash-loop.");
            return builder.With(new Win32PlatformOptions
            {
                RenderingMode = [Win32RenderingMode.Software],
                CompositionMode = [Win32CompositionMode.RedirectionSurface],
            });
        }

        Trace.WriteLine("Program: using Avalonia's default RenderingMode=[AngleEgl, Software], CompositionMode=[WinUIComposition, DirectComposition, RedirectionSurface] - interactive window station available.");
        return builder;
    }
}
