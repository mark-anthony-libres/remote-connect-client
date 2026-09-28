using System.Diagnostics;
using Avalonia.Threading;
using RemoteDesktopClient.Core.Device;
using RemoteDesktopClient.Services;
using RemoteDesktopClient.WebRTC;

namespace RemoteDesktopClient.UI.Avalonia;

public sealed partial class MainWindow
{
    private const bool DebugSessionLogCollectionEnabled = false;

    private static readonly TimeSpan DebugLogTransferTimeout = TimeSpan.FromSeconds(3);

    private void OnDeviceIdResolved(string deviceId) => Dispatcher.UIThread.Post(() =>
    {
        _localDeviceId = deviceId;
        _idChip.DeviceId = FormatDeviceId(deviceId);
    });

    private void OnConnectionStateChanged(bool isOnline) => Dispatcher.UIThread.Post(() =>
    {
        _statusBadge.IsOnline = isOnline;
        _statusBadge.InvalidateVisual();
        if (isOnline)
            _ = RefreshRecentConnectionsAsync();
    });

    private void OnConnectionRequestPending(string requestId, string targetDeviceId, string targetComputerName, DateTimeOffset expiresAt) =>
        Dispatcher.UIThread.Post(() =>
        {
            _pendingOutgoingRequestId = requestId;
            ShowWaitingForAcceptModal(requestId, targetComputerName, expiresAt);
        });

    private void OnConnectionRequestFailed(ConnectionRequestFailureReason reason, string requestId) => Dispatcher.UIThread.Post(() =>
    {
        if (_pendingOutgoingRequestId is not null && _pendingOutgoingRequestId != requestId)
            return;

        _pendingOutgoingRequestId = null;
        _remoteConnectButton.IsEnabled = !string.IsNullOrWhiteSpace(_remoteIdInput.Text);
        var (title, message) = reason switch
        {
            ConnectionRequestFailureReason.DeviceNotFound =>
                ("Device Not Found", "We couldn't find a computer with that ID.\nPlease check the ID and try again."),
            ConnectionRequestFailureReason.DeviceOffline =>
                ("Computer Offline", "The computer you're trying to connect to is currently offline."),
            ConnectionRequestFailureReason.DeviceBusy =>
                ("Computer Busy", "This computer is currently handling another connection request."),
            ConnectionRequestFailureReason.TargetDisconnected =>
                ("Connection Failed", "The computer you were connecting to disconnected before accepting."),
            ConnectionRequestFailureReason.RequestTimeout =>
                ("Connection Timed Out", "Connection request timed out. The target did not respond."),
            _ => ("Connection Failed", "Something went wrong while trying to connect."),
        };
        ShowMessageModal(title, message);
    });

    private void OnConnectionRequestDeclined(string requestId, string _) => Dispatcher.UIThread.Post(() =>
    {
        if (_pendingOutgoingRequestId != requestId)
            return;
        _pendingOutgoingRequestId = null;
        _remoteConnectButton.IsEnabled = !string.IsNullOrWhiteSpace(_remoteIdInput.Text);
        ShowMessageModal("Connection Declined", "The connection request was declined by the target computer.");
    });

    private void OnConnectionRequestAccepted(string requestId, string targetDeviceId) => Dispatcher.UIThread.Post(() =>
    {
        if (_pendingOutgoingRequestId != requestId)
            return;
        _pendingOutgoingRequestId = null;
        _requesterConnectingRequestId = requestId;
        HideModal();
        ShowLoadingModal("Connecting...", "Establishing connection...");
        _ = StartWebRtcNegotiationAsync(requestId, WebRtcRole.Answerer, "The target computer", targetDeviceId);
        _ = RefreshRecentConnectionsAsync();
    });

    private void OnIncomingConnectionRequestReceived(string requestId, string requestingDeviceId, string requestingComputerName, DateTimeOffset expiresAt) =>
        Dispatcher.UIThread.Post(() =>
        {
            _incomingRequestId = requestId;
            _incomingRequesterComputerName = requestingComputerName;
            _incomingRequesterDeviceId = requestingDeviceId;
            ShowIncomingConnectionModal(requestId, requestingComputerName, requestingDeviceId, expiresAt);
        });

    private void OnIncomingConnectionRequestCancelled(string requestId, string reason) => Dispatcher.UIThread.Post(() =>
    {
        if (_incomingRequestId != requestId)
            return;
        _incomingRequestId = null;
        if (reason == "request_timeout")
            ShowMessageModal("Request Expired", "Connection request expired.");
        else
            HideModal();
    });

    private void OnConnectionActionAcknowledged(string requestId, string action, bool success) => Dispatcher.UIThread.Post(() =>
    {
        if (action == "cancel")
        {
            if (_pendingOutgoingRequestId != requestId)
                return;
            _pendingOutgoingRequestId = null;
            _remoteConnectButton.IsEnabled = !string.IsNullOrWhiteSpace(_remoteIdInput.Text);
            HideModal();
        }
        else
        {
            if (_incomingRequestId != requestId)
                return;
            _incomingRequestId = null;
            HideModal();
            if (action == "accept" && success)
            {
                ShowLoadingModal("Connecting...", "Establishing connection...");
                var requesterLabel = string.IsNullOrWhiteSpace(_incomingRequesterComputerName) ? "A computer" : _incomingRequesterComputerName!;
                _ = StartWebRtcNegotiationAsync(requestId, WebRtcRole.Offerer, requesterLabel, _incomingRequesterDeviceId ?? string.Empty);
                _ = RefreshRecentConnectionsAsync();
            }
        }
    });

    private void OnSessionInterrupted(string requestId, SessionInterruptionReason reason) => Dispatcher.UIThread.Post(async () =>
    {
        var terminationReason = reason switch
        {
            SessionInterruptionReason.TargetDisconnected => "target_disconnected",
            SessionInterruptionReason.RequesterDisconnected => "requester_disconnected",
            _ => "interrupted",
        };
        var wasRequesterSession = _requesterConnectingRequestId == requestId;
        if (wasRequesterSession)
            _requesterConnectingRequestId = null;
        if (wasRequesterSession || _webRtcRequestId == requestId || _activeToolbarRequestId == requestId)
        {
            _webRtcEstablishingCts?.Cancel();
            _webRtcEstablishingCts = null;
            _webRtcRequestId = null;
            _webRtcRole = null;

            var isTargetSide = _activeToolbarRequestId == requestId;
            var isRequesterSide = _remoteSessionWindow is not null;
            Trace.WriteLine($"MainWindow: OnSessionInterrupted debug-log role check for {requestId} - isTargetSide={isTargetSide}, isRequesterSide={isRequesterSide}, webRtcSessionIsNull={_webRtcSession is null}");
            byte[]? receivedTargetLog = null;
            if (isTargetSide && _webRtcSession is { } targetSession)
                await SendDebugLogBeforeCloseAsync(targetSession, requestId, terminationReason);
            else if (isRequesterSide)
                receivedTargetLog = await ReceiveDebugLogBeforeCloseAsync();

            _webRtcSession?.Close(terminationReason);
            _webRtcSession = null;

            if (DebugSessionLogCollectionEnabled && isRequesterSide && _settings.ShowRemoteDebug)
                DebugSessionExporter.Export(requestId, RemoteSessionTelemetry.BuildLogFilePath(requestId), receivedTargetLog);
        }
        HideActiveSessionToolbar();
        HideRemoteSessionWindow();
        ShowMessageModal("Connection Interrupted", "Remote connection interrupted because the other computer disconnected.");
    });

    private void OnSessionEnded(string requestId) => Dispatcher.UIThread.Post(async () =>
    {
        var wasRequesterSession = _requesterConnectingRequestId == requestId;
        if (wasRequesterSession)
            _requesterConnectingRequestId = null;
        if (wasRequesterSession || _webRtcRequestId == requestId || _activeToolbarRequestId == requestId)
        {
            _webRtcEstablishingCts?.Cancel();
            _webRtcEstablishingCts = null;
            _webRtcRequestId = null;
            _webRtcRole = null;

            var isTargetSide = _activeToolbarRequestId == requestId;
            var isRequesterSide = _remoteSessionWindow is not null;
            Trace.WriteLine($"MainWindow: OnSessionEnded debug-log role check for {requestId} - isTargetSide={isTargetSide}, isRequesterSide={isRequesterSide}, webRtcSessionIsNull={_webRtcSession is null}");
            byte[]? receivedTargetLog = null;
            if (isTargetSide && _webRtcSession is { } targetSession)
                await SendDebugLogBeforeCloseAsync(targetSession, requestId, "normal");
            else if (isRequesterSide)
                receivedTargetLog = await ReceiveDebugLogBeforeCloseAsync();

            _webRtcSession?.Close();
            _webRtcSession = null;

            if (DebugSessionLogCollectionEnabled && isRequesterSide && _settings.ShowRemoteDebug)
                DebugSessionExporter.Export(requestId, RemoteSessionTelemetry.BuildLogFilePath(requestId), receivedTargetLog);
        }
        HideActiveSessionToolbar();
        HideRemoteSessionWindow();
        HideModal();
    });

    private async Task SendDebugLogBeforeCloseAsync(WebRtcSessionManager session, string requestId, string terminationReason)
    {
        if (!DebugSessionLogCollectionEnabled || !_settings.ShowRemoteDebug || _debugLogTransfer is not { } transfer || !session.IsDataChannelOpen)
        {
            Trace.WriteLine($"MainWindow: SendDebugLogBeforeCloseAsync skipped for {requestId} - ShowRemoteDebug={_settings.ShowRemoteDebug}, debugLogTransferIsNull={_debugLogTransfer is null}, isDataChannelOpen={session.IsDataChannelOpen}");
            return;
        }
        Trace.WriteLine($"MainWindow: SendDebugLogBeforeCloseAsync sending debug log for {requestId}");
        await Task.Run(() => session.FlushTelemetryForExport(terminationReason));
        await transfer.TrySendLogAsync(RemoteSessionTelemetry.BuildLogFilePath(requestId), DebugLogTransferTimeout);
    }

    private async Task<byte[]?> ReceiveDebugLogBeforeCloseAsync()
    {
        if (!DebugSessionLogCollectionEnabled || !_settings.ShowRemoteDebug || _debugLogTransfer is not { } transfer)
        {
            Trace.WriteLine($"MainWindow: ReceiveDebugLogBeforeCloseAsync skipped - ShowRemoteDebug={_settings.ShowRemoteDebug}, debugLogTransferIsNull={_debugLogTransfer is null}");
            return null;
        }
        Trace.WriteLine($"MainWindow: ReceiveDebugLogBeforeCloseAsync waiting up to {DebugLogTransferTimeout.TotalSeconds}s for the target's debug log");
        var result = await transfer.WaitForLogAsync(DebugLogTransferTimeout);
        Trace.WriteLine($"MainWindow: ReceiveDebugLogBeforeCloseAsync finished - received={result is not null}");
        return result;
    }

    private void OnSessionActive(string requestId) => Dispatcher.UIThread.Post(() =>
    {
        if (_webRtcRequestId != requestId)
            return;
        _webRtcEstablishingCts?.Cancel();
        _webRtcEstablishingCts = null;
        var role = _webRtcRole;
        var session = _webRtcSession;
        _webRtcRequestId = null;
        _webRtcRole = null;

        session?.StartTelemetry(_localDeviceId ?? string.Empty, _webRtcPeerDeviceId ?? string.Empty, _webRtcPeerLabel ?? string.Empty);

        Trace.WriteLine($"MainWindow: [WebRTC] Session ACTIVE ({role})");

        if (role == WebRtcRole.Offerer)
        {
            HideModal();
            var label = _webRtcPeerLabel ?? "A computer";
            ShowActiveSessionToolbar(requestId, $"{label} is remotely connected to this computer");
        }
        else
        {
            HideModal();
            var label = _webRtcPeerLabel ?? "the target computer";
            if (session is not null)
                ShowRemoteSessionWindow(requestId, session, label, _webRtcPeerDeviceId ?? string.Empty);
            _webRtcSessionConfirmedActive = true;
            if (session is not null)
                TrySendWebRtcTestMessage(role!.Value, session);
        }
    });

    private async Task ConnectToDeviceAsync(string targetDeviceId)
    {
        if (string.IsNullOrEmpty(targetDeviceId))
            return;
        _remoteConnectButton.IsEnabled = false;
        await _deviceIdentityClient.SendConnectionRequestAsync(targetDeviceId, Environment.MachineName);
    }

    private async void OnRemoteConnectButtonClick(object? sender, EventArgs e) =>
        await ConnectToDeviceAsync(new string(_remoteIdInput.Text.Where(char.IsDigit).ToArray()));

    private void OnRecentConnectionsCardConnectClicked(object? sender, string deviceId) =>
        _ = ConnectToDeviceAsync(new string(deviceId.Where(char.IsDigit).ToArray()));

    private void OnRecentConnectionsReceived(IReadOnlyList<RecentConnectionInfo> connections) => Dispatcher.UIThread.Post(() =>
    {
        var deviceConnections = connections.Select(info => new DeviceConnection
        {
            DeviceName = info.DeviceName ?? string.Empty,
            DeviceId = FormatDeviceId(info.DeviceId),
            Status = info.IsOnline ? ConnectionStatus.Online : ConnectionStatus.Offline,
            LastConnectedText = RelativeTimeFormatter.FormatLastConnected(info.LastConnectedAt),
            Type = DeviceType.Desktop,
        }).ToList();
        _recentConnectionsCard.UpdateDevices(deviceConnections);
    });

    private void OnDeviceStatusChanged(string deviceId, bool isOnline) => Dispatcher.UIThread.Post(() =>
        _recentConnectionsCard.UpdateDeviceStatus(FormatDeviceId(deviceId), isOnline));

    private async Task RefreshRecentConnectionsAsync()
    {
        _recentConnectionsCard.ShowLoading();
        await _deviceIdentityClient.GetRecentConnectionsAsync();
    }

    private void OnRecentConnectionsFailed() => Dispatcher.UIThread.Post(_recentConnectionsCard.ShowError);

    private void OnRecentConnectionsRetryClicked(object? sender, EventArgs e) => _ = RefreshRecentConnectionsAsync();
}
