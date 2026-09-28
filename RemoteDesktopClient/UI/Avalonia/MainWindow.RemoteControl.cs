using System.Diagnostics;
using RemoteDesktopClient.Core.Control;
using RemoteDesktopClient.WebRTC;

namespace RemoteDesktopClient.UI.Avalonia;

public sealed partial class MainWindow
{
    private void OnLocalClipboardChanged()
    {
        if (_activeToolbarRequestId is null || _webRtcSession is not { IsDataChannelOpen: true } session)
            return;

        if (_clipboard.TryGetFilePaths(out var paths))
        {
            if (_lastSyncedClipboardFiles is { } lastFiles && lastFiles.SequenceEqual(paths, StringComparer.OrdinalIgnoreCase))
                return;
            _lastSyncedClipboardFiles = paths;
            if (_fileTransfer is { } fileTransfer)
                _ = fileTransfer.SendFilesAsync(paths);
            return;
        }
        _lastSyncedClipboardFiles = null;

        if (_clipboard.TryGetImagePng(out var png))
        {
            if (_lastSyncedClipboardImage is { } lastImage && lastImage.AsSpan().SequenceEqual(png))
                return;
            _lastSyncedClipboardImage = png;
            _ = ClipboardImageChunker.SendAsync(png, _settings.ClipboardImageMaxBitrateKbps, session.SendControlMessage);
            return;
        }

        if (_clipboard.TryGetText(out var text))
        {
            if (text == _lastSyncedClipboardText)
                return;
            _lastSyncedClipboardText = text;
            session.SendControlMessage(new ClipboardTextMessage(text));
        }
    }

    private void HandleNonMouseControlMessage(ControlMessage message, WebRtcRole role, string requestId, ClipboardImageReassembler clipboardImageReassembler, ClipboardFileTransfer fileTransfer, DebugSessionLogTransfer debugLogTransfer, WebRtcSessionManager session)
    {
        switch (message)
        {
            case KeyDownMessage or KeyUpMessage:
                if (role != WebRtcRole.Offerer || _activeToolbarRequestId != requestId)
                    return;
                ApplyInputMessage(message);
                break;

            case ClipboardTextMessage:
                if (IsThisSideActiveForClipboard(role, requestId))
                    ApplyClipboardMessage(message, role);
                break;

            case ClipboardImageChunkMessage chunk:
                if (IsThisSideActiveForClipboard(role, requestId) && clipboardImageReassembler.Add(chunk) is { } completePng)
                    ApplyClipboardMessage(new ClipboardImageMessage(completePng), role);
                break;

            case FileTransferStartMessage or FileTransferChunkMessage or FileTransferCompleteMessage
                or FileTransferCancelMessage or FileTransferErrorMessage:
                if (IsThisSideActiveForClipboard(role, requestId))
                    fileTransfer.Handle(message);
                break;

            case DebugLogStartMessage or DebugLogChunkMessage or DebugLogCompleteMessage:
                var gatePassed = IsThisSideActiveForClipboard(role, requestId);
                Trace.WriteLine($"MainWindow: received {message.GetType().Name} for {requestId} - gate_passed={gatePassed} (role={role}, activeToolbarRequestId={_activeToolbarRequestId}, remoteSessionWindowIsNull={_remoteSessionWindow is null})");
                if (gatePassed)
                    debugLogTransfer.Handle(message);
                break;

            case AdjustVideoSettingsMessage adjust:
                if (role == WebRtcRole.Offerer && _activeToolbarRequestId == requestId)
                    _ = session.ApplyLiveVideoSettingsAsync(adjust.Fps, adjust.BitrateKbps);
                break;
        }
    }

    private void ApplyInputMessage(ControlMessage message)
    {
        switch (message)
        {
            case MouseMoveMessage move:
                _inputInjector.MoveMouse(move.NormalizedX, move.NormalizedY);
                break;
            case MouseButtonMessage button:
                _inputInjector.MouseButton(button.Button, button.Action, button.NormalizedX, button.NormalizedY);
                break;
            case MouseWheelMessage wheel:
                _inputInjector.MouseWheel(wheel.NormalizedX, wheel.NormalizedY, wheel.DeltaY);
                break;
            case KeyDownMessage keyDown:
                _inputInjector.KeyDown(keyDown.Key);
                break;
            case KeyUpMessage keyUp:
                _inputInjector.KeyUp(keyUp.Key);
                break;
        }
    }

    private bool IsThisSideActiveForClipboard(WebRtcRole role, string requestId) => role == WebRtcRole.Offerer
        ? _activeToolbarRequestId == requestId
        : _remoteSessionWindow is not null;

    private void ApplyClipboardMessage(ControlMessage message, WebRtcRole role)
    {
        if (role == WebRtcRole.Offerer)
        {
            switch (message)
            {
                case ClipboardTextMessage text:
                    _lastSyncedClipboardText = text.Text;
                    _clipboard.SetText(text.Text);
                    break;
                case ClipboardImageMessage image:
                    _lastSyncedClipboardImage = image.PngBytes;
                    _clipboard.SetImagePng(image.PngBytes);
                    break;
                case ClipboardFilesMessage files:
                    _lastSyncedClipboardFiles = files.Paths;
                    _clipboard.SetFilePaths(files.Paths);
                    break;
            }
        }
        else
        {
            _remoteSessionWindow?.ApplyRemoteClipboard(message);
        }
    }
}
