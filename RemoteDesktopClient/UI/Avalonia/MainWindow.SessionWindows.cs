using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using RemoteDesktopClient.Core.Control;
using RemoteDesktopClient.Native.Windows;
using RemoteDesktopClient.UI.Avalonia.Components.Shared;
using RemoteDesktopClient.WebRTC;

namespace RemoteDesktopClient.UI.Avalonia;

public sealed partial class MainWindow
{
    private void ActivateForRelaunch()
    {
        Window target = _remoteSessionWindow is { } remoteWindow
            ? remoteWindow
            : _activeSessionToolbar is { } toolbar
                ? toolbar
                : this;

        if (target.WindowState == WindowState.Minimized)
            target.WindowState = WindowState.Normal;
        target.Show();
        target.Activate();

        if (OperatingSystem.IsWindows() && target.TryGetPlatformHandle() is { } platformHandle)
            Win32ForegroundActivator.ForceToForeground(platformHandle.Handle);
    }

    private void ShowActiveSessionToolbar(string requestId, string statusText)
    {
        if (_activeSessionToolbar is not null)
            return;

        _activeToolbarRequestId = requestId;
        _activeSessionToolbar = new ActiveSessionToolbar(statusText);
        _activeSessionToolbar.EndSessionRequested += (_, _) => EndTargetSession();
        _activeSessionToolbar.Closed += (_, _) => _activeSessionToolbar = null;

        Hide();
        _activeSessionToolbar.Show();
    }

    private void HideActiveSessionToolbar()
    {
        _activeToolbarRequestId = null;
        _remoteConnectButton.IsEnabled = !string.IsNullOrWhiteSpace(_remoteIdInput.Text);

        if (_activeSessionToolbar is null)
            return;

        _activeSessionToolbar.Close();
        _activeSessionToolbar = null;

        Show();
        Activate();
    }

    private const int ThumbnailCaptureEveryNFrames = 150;
    private static readonly PixelSize ThumbnailSize = new(504, 184);

    private void ShowRemoteSessionWindow(string requestId, WebRtcSessionManager session, string peerLabel, string targetDeviceId)
    {
        if (_remoteSessionWindow is not null)
            return;

        var window = new RemoteSessionWindow(peerLabel, _settings.ShowRemoteDebug);
        _remoteSessionWindow = window;

        DispatcherTimer? debugStatsTimer = null;
        if (_settings.ShowRemoteDebug)
        {
            debugStatsTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            debugStatsTimer.Tick += (_, _) =>
            {
                var stats = session.SampleVideoStats();
                window.UpdateDebugStats(stats.HasVideo, stats.BitrateKbps, stats.Fps, stats.IsRelayed);
            };
            debugStatsTimer.Start();
        }

        session.VideoFrameReceived += (sample, width, height, stride, pixelFormat) => Dispatcher.UIThread.Post(() =>
        {
            if (!ReferenceEquals(_remoteSessionWindow, window))
                return;
            window.UpdateFrame(sample, width, height, stride, pixelFormat);
        });
        window.FrameRendered += session.RecordFrameRendered;

        if (_settings.EnableSessionThumbnails && !string.IsNullOrEmpty(targetDeviceId))
        {
            var thumbnailFrameCount = 0;
            window.FrameRendered += () =>
            {
                thumbnailFrameCount++;
                if (thumbnailFrameCount % ThumbnailCaptureEveryNFrames != 0)
                    return;
                if (window.CurrentFrame is not { } frame)
                    return;
                var thumbnail = new RenderTargetBitmap(ThumbnailSize);
                using (var context = thumbnail.CreateDrawingContext())
                    context.DrawImage(frame, new Rect(0, 0, ThumbnailSize.Width, ThumbnailSize.Height));
                _ = Task.Run(() =>
                {
                    try { DeviceThumbnailCache.Save(targetDeviceId, thumbnail); }
                    finally { thumbnail.Dispose(); }
                });
            };
        }
        window.ControlMessageCaptured += message =>
        {
            if (!session.IsDataChannelOpen)
                return;
            if (message is ClipboardImageMessage image)
                _ = ClipboardImageChunker.SendAsync(image.PngBytes, _settings.ClipboardImageMaxBitrateKbps, session.SendControlMessage);
            else if (message is ClipboardFilesMessage files)
                _ = _fileTransfer?.SendFilesAsync(files.Paths);
            else
                session.SendControlMessage(message);
        };
        window.LiveVideoSettingsRequested += (fps, bitrateKbps) =>
        {
            if (session.IsDataChannelOpen)
                session.SendControlMessage(new AdjustVideoSettingsMessage(fps, bitrateKbps));
        };
        window.Closed += async (_, _) =>
        {
            debugStatsTimer?.Stop();

            if (!string.IsNullOrEmpty(targetDeviceId))
                _recentConnectionsCard.RefreshDeviceThumbnail(FormatDeviceId(targetDeviceId));

            if (!ReferenceEquals(_remoteSessionWindow, window))
                return;

            var wasClosedProgrammatically = _closingRemoteSessionWindowProgrammatically;
            _closingRemoteSessionWindowProgrammatically = false;
            if (wasClosedProgrammatically)
            {
                _remoteSessionWindow = null;
                return;
            }

            if (_requesterConnectingRequestId == requestId)
                _requesterConnectingRequestId = null;
            _webRtcEstablishingCts?.Cancel();
            _webRtcEstablishingCts = null;
            _webRtcRequestId = null;
            _webRtcRole = null;

            var receivedTargetLog = await ReceiveDebugLogBeforeCloseAsync();
            _webRtcSession?.Close();
            _webRtcSession = null;
            _remoteSessionWindow = null;
            if (DebugSessionLogCollectionEnabled && _settings.ShowRemoteDebug)
                DebugSessionExporter.Export(requestId, RemoteSessionTelemetry.BuildLogFilePath(requestId), receivedTargetLog);

            _ = _deviceIdentityClient.SendEndSessionAsync(requestId);

            Show();
            Activate();
        };

        Hide();
        window.Show();
    }

    private void HideRemoteSessionWindow()
    {
        if (_remoteSessionWindow is null)
            return;

        _closingRemoteSessionWindowProgrammatically = true;
        _remoteSessionWindow.Close();
        _remoteSessionWindow = null;

        Show();
        Activate();
    }

    private void ShowReceivedDataChannelMessage(string text) => new WebRtcTestMessageAlert(text).Show();

    private void ShowFileTransferAlert(FileTransferAlert alert) => new FileTransferAlertWindow(alert.Title, alert.Message).Show();

    private async void EndTargetSession()
    {
        var requestId = _webRtcRequestId ?? _activeToolbarRequestId;
        var session = _webRtcSession;

        _webRtcEstablishingCts?.Cancel();
        _webRtcEstablishingCts = null;
        _webRtcRequestId = null;
        _webRtcRole = null;

        if (session is not null && requestId is not null)
            await SendDebugLogBeforeCloseAsync(session, requestId, "normal");

        session?.Close();
        _webRtcSession = null;

        if (requestId is not null)
            _ = _deviceIdentityClient.SendEndSessionAsync(requestId);

        HideModal();
        HideActiveSessionToolbar();
    }
}
