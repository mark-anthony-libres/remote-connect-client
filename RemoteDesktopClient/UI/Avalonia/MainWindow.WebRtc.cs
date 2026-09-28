using System.Diagnostics;
using Avalonia.Threading;
using RemoteDesktopClient.Core.Control;
using RemoteDesktopClient.WebRTC;

namespace RemoteDesktopClient.UI.Avalonia;

public sealed partial class MainWindow
{
    private async Task StartWebRtcNegotiationAsync(string requestId, WebRtcRole role, string peerLabel, string remoteDeviceId)
    {
        _webRtcRequestId = requestId;
        _webRtcRole = role;
        _webRtcPeerLabel = peerLabel;
        _webRtcPeerDeviceId = remoteDeviceId;
        _webRtcSessionConfirmedActive = false;
        _webRtcTestMessageSent = false;

        var session = new WebRtcSessionManager(_signalingClient, requestId, role, _settings);
        _webRtcSession = session;
        var clipboardImageReassembler = new ClipboardImageReassembler();

        var fileTransfer = new ClipboardFileTransfer(
            isTarget: role == WebRtcRole.Offerer,
            maxTurnMb: _settings.FileTransferMaxTurnMb,
            isRelayed: () => session.IsUsingRelay,
            send: session.SendControlMessage,
            bufferedAmount: () => session.DataChannelBufferedAmount,
            stagingRoot: ClipboardFileTransfer.DefaultStagingRoot);
        _fileTransfer?.Dispose();
        _fileTransfer = fileTransfer;
        fileTransfer.FilesReceived += paths => Dispatcher.UIThread.Post(() =>
        {
            if (IsThisSideActiveForClipboard(role, requestId))
                ApplyClipboardMessage(new ClipboardFilesMessage(paths), role);
        });
        fileTransfer.AlertRaised += alert => Dispatcher.UIThread.Post(() => ShowFileTransferAlert(alert));
        session.Closed += () =>
        {
            fileTransfer.Dispose();
            if (ReferenceEquals(_fileTransfer, fileTransfer))
                _fileTransfer = null;
        };

        var debugLogTransfer = new DebugSessionLogTransfer(
            isTarget: role == WebRtcRole.Offerer,
            send: session.SendControlMessage,
            bufferedAmount: () => session.DataChannelBufferedAmount);
        _debugLogTransfer = debugLogTransfer;
        session.Closed += () =>
        {
            if (ReferenceEquals(_debugLogTransfer, debugLogTransfer))
                _debugLogTransfer = null;
        };

        session.DataChannelOpened += () => Dispatcher.UIThread.Post(() =>
        {
            Trace.WriteLine($"MainWindow: [WebRTC] DataChannel OPEN ({role})");
            _ = _deviceIdentityClient.SendWebRtcConnectedAsync(requestId);
            TrySendWebRtcTestMessage(role, session);
        });
        session.MessageReceived += text => Dispatcher.UIThread.Post(() =>
        {
            Trace.WriteLine($"MainWindow: [WebRTC] Received: {text}");
            if (role == WebRtcRole.Offerer)
                ShowReceivedDataChannelMessage(text);
        });
        session.ControlMessageReceived += message =>
        {
            if (message is MouseMoveMessage or MouseButtonMessage or MouseWheelMessage)
            {
                if (role == WebRtcRole.Offerer && _activeToolbarRequestId == requestId)
                    ApplyInputMessage(message);
                return;
            }

            Dispatcher.UIThread.Post(() => HandleNonMouseControlMessage(message, role, requestId, clipboardImageReassembler, fileTransfer, debugLogTransfer, session));
        };
        session.StageChanged += stage => Dispatcher.UIThread.Post(() =>
        {
            Trace.WriteLine($"MainWindow: [WebRTC] stage: {stage}");
            if (_webRtcRequestId != requestId)
                return;
            ShowLoadingModal("Connecting...", WebRtcStageStatusText(stage));
        });
        session.Failed += reason => Dispatcher.UIThread.Post(() =>
        {
            if (!ReferenceEquals(_webRtcSession, session))
                return;
            if (_webRtcRequestId == requestId)
                FailWebRtcNegotiation(requestId, role);
            else
                _ = _deviceIdentityClient.SendWebRtcFailedAsync(requestId);
        });

        var cts = new CancellationTokenSource();
        _webRtcEstablishingCts?.Cancel();
        _webRtcEstablishingCts = cts;
        _ = RunWebRtcEstablishmentTimeoutAsync(requestId, role, session, cts.Token);

        try
        {
            await session.StartAsync();
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"MainWindow: WebRTC StartAsync threw for {requestId}. {ex}");
            Dispatcher.UIThread.Post(() => FailWebRtcNegotiation(requestId, role));
        }
    }

    private void TrySendWebRtcTestMessage(WebRtcRole role, WebRtcSessionManager session)
    {
        if (role != WebRtcRole.Answerer)
            return;
        if (!ReferenceEquals(_webRtcSession, session))
            return;
        if (_webRtcTestMessageSent || !_webRtcSessionConfirmedActive || !session.IsDataChannelOpen)
            return;

        _webRtcTestMessageSent = true;
        Trace.WriteLine("MainWindow: [WebRTC] Sending: hello from requester");
        try
        {
            session.SendText("hello from requester");
        }
        catch (InvalidOperationException ex)
        {
            Trace.WriteLine($"MainWindow: failed to send the post-ACTIVE test message. {ex}");
            ShowMessageModal("Send Failed", "Connected, but sending the test message over the WebRTC DataChannel failed.\n" + ex.Message);
        }
    }

    private static string WebRtcStageStatusText(WebRtcStage stage) => stage switch
    {
        WebRtcStage.OfferSent => "Offer sent...",
        WebRtcStage.OfferReceived => "Offer received...",
        WebRtcStage.AnswerSent => "Answer sent...",
        WebRtcStage.AnswerReceived => "Answer received...",
        WebRtcStage.IceConnected => "ICE connected...",
        WebRtcStage.DataChannelConnected => "DataChannel connected...",
        _ => "Establishing connection...",
    };

    private async Task RunWebRtcEstablishmentTimeoutAsync(string requestId, WebRtcRole role, WebRtcSessionManager session, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(WebRtcEstablishmentTimeout, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        Dispatcher.UIThread.Post(() =>
        {
            if (!ReferenceEquals(_webRtcSession, session) || _webRtcRequestId != requestId)
                return;
            Trace.WriteLine($"MainWindow: [WebRTC] establishment timed out after {WebRtcEstablishmentTimeout.TotalSeconds}s for {requestId}");
            FailWebRtcNegotiation(requestId, role);
        });
    }

    private void FailWebRtcNegotiation(string requestId, WebRtcRole role)
    {
        if (_webRtcRequestId != requestId)
            return;

        _webRtcEstablishingCts?.Cancel();
        _webRtcEstablishingCts = null;
        _webRtcRequestId = null;
        _webRtcRole = null;
        _webRtcSession?.Close();
        _webRtcSession = null;

        if (role == WebRtcRole.Answerer)
        {
            _requesterConnectingRequestId = null;
            _remoteConnectButton.IsEnabled = !string.IsNullOrWhiteSpace(_remoteIdInput.Text);
        }

        HideModal();
        ShowMessageModal("Connection Failed", "Unable to establish the WebRTC connection.\nPlease try again.");
    }
}
