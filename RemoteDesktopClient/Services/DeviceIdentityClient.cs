using System.Diagnostics;
using System.Net.Http;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RemoteDesktopClient.Services;

public enum ConnectionRequestFailureReason
{
    DeviceNotFound,
    DeviceOffline,
    DeviceBusy,
    TargetDisconnected,
    RequestTimeout,
}

public sealed record RecentConnectionInfo(string DeviceId, string? DeviceName, DateTimeOffset LastConnectedAt, bool IsOnline);

public enum SessionInterruptionReason
{
    TargetDisconnected,
    RequesterDisconnected,
}

public sealed class DeviceIdentityClient(string deviceWebSocketUrl)
{
    private static readonly TimeSpan ReconnectDelay = TimeSpan.FromSeconds(5);

    private ClientWebSocket? _activeWebSocket;

    private readonly HttpClient _httpClient = new();

    public event Action<string>? DeviceIdResolved;

    public event Action<bool>? ConnectionStateChanged;

    public event Action<string, string, string, DateTimeOffset>? ConnectionRequestPending;

    public event Action<ConnectionRequestFailureReason, string>? ConnectionRequestFailed;

    public event Action<string, string, string, DateTimeOffset>? IncomingConnectionRequestReceived;

    public event Action<string, string>? ConnectionRequestAccepted;

    public event Action<string, string>? ConnectionRequestDeclined;

    public event Action<string, string>? IncomingConnectionRequestCancelled;

    public event Action<string, string, bool>? ConnectionActionAcknowledged;

    public event Action<string, SessionInterruptionReason>? SessionInterrupted;

    public event Action<string>? SessionEnded;

    public event Action<string, string, string>? WebRtcOfferReceived;

    public event Action<string, string, string>? WebRtcAnswerReceived;

    public event Action<string, string, string>? WebRtcIceCandidateReceived;

    public event Action<string>? SessionActive;

    public event Action<IReadOnlyList<RecentConnectionInfo>>? RecentConnectionsReceived;

    public event Action? RecentConnectionsFailed;

    public event Action<string, bool>? DeviceStatusChanged;

    public async Task KeepConnectionAliveAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await ConnectOnceAndListenAsync(cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"DeviceIdentityClient: connection to {deviceWebSocketUrl} failed, retrying in {ReconnectDelay}. {ex}");
            }

            ConnectionStateChanged?.Invoke(false);

            try
            {
                await Task.Delay(ReconnectDelay, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    public Task SendConnectionRequestAsync(string targetDeviceId, string requestingComputerName, CancellationToken cancellationToken = default) =>
        SendMessageAsync(new ConnectionRequestMessage(targetDeviceId, requestingComputerName), cancellationToken);

    public Task AcceptConnectionRequestAsync(string requestId, CancellationToken cancellationToken = default) =>
        SendMessageAsync(new ConnectionActionMessage("connection-accept", requestId), cancellationToken);

    public Task DeclineConnectionRequestAsync(string requestId, CancellationToken cancellationToken = default) =>
        SendMessageAsync(new ConnectionActionMessage("connection-decline", requestId), cancellationToken);

    public Task CancelConnectionRequestAsync(string requestId, CancellationToken cancellationToken = default) =>
        SendMessageAsync(new ConnectionActionMessage("connection-cancel", requestId), cancellationToken);

    public Task SendEndSessionAsync(string requestId, CancellationToken cancellationToken = default) =>
        SendMessageAsync(new ConnectionActionMessage("end-session", requestId), cancellationToken);

    public Task SendWebRtcOfferAsync(string requestId, string sdp, CancellationToken cancellationToken = default) =>
        SendMessageAsync(new WebRtcSdpMessage("webrtc-offer", requestId, sdp), cancellationToken);

    public Task SendWebRtcAnswerAsync(string requestId, string sdp, CancellationToken cancellationToken = default) =>
        SendMessageAsync(new WebRtcSdpMessage("webrtc-answer", requestId, sdp), cancellationToken);

    public Task SendWebRtcIceCandidateAsync(string requestId, string candidateJson, CancellationToken cancellationToken = default) =>
        SendMessageAsync(new WebRtcIceCandidateMessage(requestId, JsonDocument.Parse(candidateJson).RootElement.Clone()), cancellationToken);

    public Task SendWebRtcConnectedAsync(string requestId, CancellationToken cancellationToken = default) =>
        SendMessageAsync(new ConnectionActionMessage("webrtc-connected", requestId), cancellationToken);

    public Task SendWebRtcFailedAsync(string requestId, CancellationToken cancellationToken = default) =>
        SendMessageAsync(new ConnectionActionMessage("webrtc-failed", requestId), cancellationToken);

    public async Task GetRecentConnectionsAsync(CancellationToken cancellationToken = default)
    {
        var installKey = DeviceIdentityStore.LoadInstallKey();
        if (installKey is null)
            return;

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, BuildRecentConnectionsUri());
            request.Headers.Add("X-Install-Key", installKey);
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                Trace.WriteLine($"DeviceIdentityClient: GetRecentConnectionsAsync got HTTP {(int)response.StatusCode}.");
                RecentConnectionsFailed?.Invoke();
                return;
            }

            using var document = JsonDocument.Parse(await response.Content.ReadAsStreamAsync(cancellationToken));
            HandleRecentConnections(document.RootElement);
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"DeviceIdentityClient: GetRecentConnectionsAsync failed. {ex}");
            RecentConnectionsFailed?.Invoke();
        }
    }

    internal Uri BuildRecentConnectionsUri()
    {
        var wsUri = new Uri(deviceWebSocketUrl);
        const string wsSuffix = "/ws/device";
        var apiPrefixPath = wsUri.AbsolutePath.EndsWith(wsSuffix, StringComparison.Ordinal)
            ? wsUri.AbsolutePath[..^wsSuffix.Length]
            : wsUri.AbsolutePath;
        var builder = new UriBuilder(wsUri)
        {
            Scheme = wsUri.Scheme == "wss" ? "https" : "http",
            Path = apiPrefixPath.TrimEnd('/') + "/devices/recent-connections",
        };
        return builder.Uri;
    }

    private async Task SendMessageAsync<T>(T message, CancellationToken cancellationToken)
    {
        var webSocket = _activeWebSocket;
        if (webSocket is null || webSocket.State != WebSocketState.Open)
        {
            Trace.WriteLine($"DeviceIdentityClient: tried to send a {typeof(T).Name} with no open connection.");
            return;
        }

        try
        {
            var json = JsonSerializer.Serialize(message);
            var bytes = Encoding.UTF8.GetBytes(json);
            await webSocket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, cancellationToken);
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"DeviceIdentityClient: failed to send {typeof(T).Name}. {ex}");
        }
    }

    private async Task ConnectOnceAndListenAsync(CancellationToken cancellationToken)
    {
        using var webSocket = new ClientWebSocket();
        _activeWebSocket = webSocket;
        try
        {
            await webSocket.ConnectAsync(new Uri(deviceWebSocketUrl), cancellationToken);

            var handshakeJson = JsonSerializer.Serialize(new HandshakeRequest(DeviceIdentityStore.LoadInstallKey(), Environment.MachineName));
            var handshakeBytes = Encoding.UTF8.GetBytes(handshakeJson);
            await webSocket.SendAsync(new ArraySegment<byte>(handshakeBytes), WebSocketMessageType.Text, true, cancellationToken);

            var handshakeResponseJson = await ReadFullMessageAsync(webSocket, cancellationToken);
            if (handshakeResponseJson is null)
                return;

            var deviceIdentity = JsonSerializer.Deserialize<IdentityResponse>(handshakeResponseJson);
            if (deviceIdentity?.DeviceId is null)
                return;

            if (deviceIdentity.InstallKey is not null)
                DeviceIdentityStore.SaveInstallKey(deviceIdentity.InstallKey);

            DeviceIdResolved?.Invoke(deviceIdentity.DeviceId);
            ConnectionStateChanged?.Invoke(true);

            while (!cancellationToken.IsCancellationRequested)
            {
                var messageJson = await ReadFullMessageAsync(webSocket, cancellationToken);
                if (messageJson is null)
                    break;
                HandleIncomingMessage(messageJson);
            }
        }
        finally
        {
            if (ReferenceEquals(_activeWebSocket, webSocket))
                _activeWebSocket = null;
        }
    }

    private void HandleIncomingMessage(string messageJson)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(messageJson);
        }
        catch (JsonException ex)
        {
            Trace.WriteLine($"DeviceIdentityClient: received malformed JSON message. {ex}");
            return;
        }

        using (document)
        {
            if (!document.RootElement.TryGetProperty("type", out var typeProperty))
                return;

            switch (typeProperty.GetString())
            {
                case "connection-request-pending":
                    HandleConnectionRequestPending(document.RootElement);
                    break;
                case "connection-request":
                    HandleIncomingConnectionRequest(document.RootElement);
                    break;
                case "connection-accepted":
                    HandleTargetOutcome(document.RootElement, ConnectionRequestAccepted);
                    break;
                case "connection-declined":
                    HandleTargetOutcome(document.RootElement, ConnectionRequestDeclined);
                    break;
                case "connection-cancelled":
                    HandleIncomingConnectionCancelled(document.RootElement);
                    break;
                case "connection-failed":
                    HandleConnectionFailed(document.RootElement);
                    break;
                case "connection-action-ack":
                    HandleConnectionActionAck(document.RootElement);
                    break;
                case "session-interrupted":
                    HandleSessionInterrupted(document.RootElement);
                    break;
                case "session-ended":
                    HandleSessionEnded(document.RootElement);
                    break;
                case "session-active":
                    HandleSessionActive(document.RootElement);
                    break;
                case "webrtc-offer":
                    HandleWebRtcSignal(document.RootElement, WebRtcOfferReceived);
                    break;
                case "webrtc-answer":
                    HandleWebRtcSignal(document.RootElement, WebRtcAnswerReceived);
                    break;
                case "webrtc-ice-candidate":
                    HandleWebRtcIceCandidate(document.RootElement);
                    break;
                case "device-status-changed":
                    HandleDeviceStatusChanged(document.RootElement);
                    break;
            }
        }
    }

    private static void HandleTargetOutcome(JsonElement root, Action<string, string>? evt)
    {
        var requestId = root.TryGetProperty("request_id", out var requestIdProperty) ? requestIdProperty.GetString() : null;
        var targetDeviceId = root.TryGetProperty("target_device_id", out var targetProperty) ? targetProperty.GetString() : null;
        if (string.IsNullOrEmpty(requestId) || string.IsNullOrEmpty(targetDeviceId))
            return;
        evt?.Invoke(requestId, targetDeviceId);
    }

    private void HandleConnectionRequestPending(JsonElement root)
    {
        var requestId = root.TryGetProperty("request_id", out var requestIdProperty) ? requestIdProperty.GetString() : null;
        var targetDeviceId = root.TryGetProperty("target_device_id", out var targetProperty) ? targetProperty.GetString() : null;
        if (string.IsNullOrEmpty(requestId) || string.IsNullOrEmpty(targetDeviceId) || !TryGetExpiresAt(root, out var expiresAt))
            return;

        var targetComputerName = root.TryGetProperty("target_computer_name", out var nameProperty) ? nameProperty.GetString() : null;
        ConnectionRequestPending?.Invoke(requestId, targetDeviceId, targetComputerName ?? string.Empty, expiresAt);
    }

    private void HandleIncomingConnectionRequest(JsonElement root)
    {
        var requestId = root.TryGetProperty("request_id", out var requestIdProperty) ? requestIdProperty.GetString() : null;
        var requestingDeviceId = root.TryGetProperty("requesting_device_id", out var idProperty) ? idProperty.GetString() : null;
        if (string.IsNullOrEmpty(requestId) || string.IsNullOrEmpty(requestingDeviceId) || !TryGetExpiresAt(root, out var expiresAt))
            return;

        var requestingComputerName = root.TryGetProperty("requesting_computer_name", out var nameProperty) ? nameProperty.GetString() : null;
        IncomingConnectionRequestReceived?.Invoke(requestId, requestingDeviceId, requestingComputerName ?? string.Empty, expiresAt);
    }

    private void HandleIncomingConnectionCancelled(JsonElement root)
    {
        var requestId = root.TryGetProperty("request_id", out var requestIdProperty) ? requestIdProperty.GetString() : null;
        if (string.IsNullOrEmpty(requestId))
            return;
        var reason = root.TryGetProperty("reason", out var reasonProperty) ? reasonProperty.GetString() : null;
        IncomingConnectionRequestCancelled?.Invoke(requestId, reason ?? string.Empty);
    }

    private static bool TryGetExpiresAt(JsonElement root, out DateTimeOffset expiresAt)
    {
        if (root.TryGetProperty("expires_at", out var expiresAtProperty)
            && expiresAtProperty.ValueKind == JsonValueKind.String
            && DateTimeOffset.TryParse(expiresAtProperty.GetString(), out expiresAt))
        {
            return true;
        }
        expiresAt = default;
        return false;
    }

    private void HandleConnectionFailed(JsonElement root)
    {
        var requestId = root.TryGetProperty("request_id", out var requestIdProperty) ? requestIdProperty.GetString() : null;
        var reason = root.TryGetProperty("reason", out var reasonProperty) ? reasonProperty.GetString() : null;
        switch (reason)
        {
            case "device_not_found":
                ConnectionRequestFailed?.Invoke(ConnectionRequestFailureReason.DeviceNotFound, requestId ?? string.Empty);
                break;
            case "device_offline":
                ConnectionRequestFailed?.Invoke(ConnectionRequestFailureReason.DeviceOffline, requestId ?? string.Empty);
                break;
            case "device_busy":
                ConnectionRequestFailed?.Invoke(ConnectionRequestFailureReason.DeviceBusy, requestId ?? string.Empty);
                break;
            case "target_disconnected":
                ConnectionRequestFailed?.Invoke(ConnectionRequestFailureReason.TargetDisconnected, requestId ?? string.Empty);
                break;
            case "request_timeout":
                ConnectionRequestFailed?.Invoke(ConnectionRequestFailureReason.RequestTimeout, requestId ?? string.Empty);
                break;
            case "malformed_request":
                Trace.WriteLine("DeviceIdentityClient: server reported our connection request was malformed.");
                break;
            default:
                Trace.WriteLine($"DeviceIdentityClient: received connection-failed with unrecognized reason '{reason}'.");
                break;
        }
    }

    private void HandleConnectionActionAck(JsonElement root)
    {
        var requestId = root.TryGetProperty("request_id", out var requestIdProperty) ? requestIdProperty.GetString() : null;
        var action = root.TryGetProperty("action", out var actionProperty) ? actionProperty.GetString() : null;
        var success = root.TryGetProperty("success", out var successProperty) && successProperty.ValueKind == JsonValueKind.True;
        if (string.IsNullOrEmpty(requestId) || string.IsNullOrEmpty(action))
            return;
        ConnectionActionAcknowledged?.Invoke(requestId, action, success);
    }

    private void HandleSessionInterrupted(JsonElement root)
    {
        var requestId = root.TryGetProperty("request_id", out var requestIdProperty) ? requestIdProperty.GetString() : null;
        var terminationReason = root.TryGetProperty("termination_reason", out var reasonProperty) ? reasonProperty.GetString() : null;
        if (string.IsNullOrEmpty(requestId))
            return;

        var reason = terminationReason switch
        {
            "target_disconnected" => SessionInterruptionReason.TargetDisconnected,
            "requester_disconnected" => SessionInterruptionReason.RequesterDisconnected,
            _ => (SessionInterruptionReason?)null,
        };
        if (reason is null)
        {
            Trace.WriteLine($"DeviceIdentityClient: received session-interrupted with unrecognized termination_reason '{terminationReason}'.");
            return;
        }
        SessionInterrupted?.Invoke(requestId, reason.Value);
    }

    private void HandleSessionEnded(JsonElement root)
    {
        var requestId = root.TryGetProperty("request_id", out var requestIdProperty) ? requestIdProperty.GetString() : null;
        if (string.IsNullOrEmpty(requestId))
            return;
        SessionEnded?.Invoke(requestId);
    }

    private void HandleSessionActive(JsonElement root)
    {
        var requestId = root.TryGetProperty("request_id", out var requestIdProperty) ? requestIdProperty.GetString() : null;
        if (string.IsNullOrEmpty(requestId))
            return;
        SessionActive?.Invoke(requestId);
    }

    private static void HandleWebRtcSignal(JsonElement root, Action<string, string, string>? evt)
    {
        var requestId = root.TryGetProperty("request_id", out var requestIdProperty) ? requestIdProperty.GetString() : null;
        var fromDeviceId = root.TryGetProperty("from_device_id", out var fromProperty) ? fromProperty.GetString() : null;
        var sdp = root.TryGetProperty("sdp", out var sdpProperty) ? sdpProperty.GetString() : null;
        if (string.IsNullOrEmpty(requestId) || string.IsNullOrEmpty(fromDeviceId) || sdp is null)
            return;
        evt?.Invoke(requestId, fromDeviceId, sdp);
    }

    private void HandleWebRtcIceCandidate(JsonElement root)
    {
        var requestId = root.TryGetProperty("request_id", out var requestIdProperty) ? requestIdProperty.GetString() : null;
        var fromDeviceId = root.TryGetProperty("from_device_id", out var fromProperty) ? fromProperty.GetString() : null;
        if (string.IsNullOrEmpty(requestId) || string.IsNullOrEmpty(fromDeviceId))
            return;
        if (!root.TryGetProperty("candidate", out var candidateProperty))
            return;
        WebRtcIceCandidateReceived?.Invoke(requestId, fromDeviceId, candidateProperty.GetRawText());
    }

    private void HandleRecentConnections(JsonElement root)
    {
        if (!root.TryGetProperty("connections", out var connectionsProperty) || connectionsProperty.ValueKind != JsonValueKind.Array)
            return;

        var connections = new List<RecentConnectionInfo>();
        foreach (var entry in connectionsProperty.EnumerateArray())
        {
            var deviceId = entry.TryGetProperty("device_id", out var idProperty) ? idProperty.GetString() : null;
            if (string.IsNullOrEmpty(deviceId))
                continue;

            var deviceName = entry.TryGetProperty("device_name", out var nameProperty) && nameProperty.ValueKind == JsonValueKind.String
                ? nameProperty.GetString()
                : null;

            if (!entry.TryGetProperty("last_connected_at", out var lastConnectedProperty)
                || lastConnectedProperty.ValueKind != JsonValueKind.String
                || !DateTimeOffset.TryParse(lastConnectedProperty.GetString(), out var lastConnectedAt))
                continue;

            var isOnline = entry.TryGetProperty("is_online", out var onlineProperty) && onlineProperty.ValueKind == JsonValueKind.True;
            connections.Add(new RecentConnectionInfo(deviceId, deviceName, lastConnectedAt, isOnline));
        }
        RecentConnectionsReceived?.Invoke(connections);
    }

    private void HandleDeviceStatusChanged(JsonElement root)
    {
        var deviceId = root.TryGetProperty("device_id", out var idProperty) ? idProperty.GetString() : null;
        if (string.IsNullOrEmpty(deviceId))
            return;

        var isOnline = root.TryGetProperty("is_online", out var onlineProperty) && onlineProperty.ValueKind == JsonValueKind.True;
        DeviceStatusChanged?.Invoke(deviceId, isOnline);
    }

    private static async Task<string?> ReadFullMessageAsync(ClientWebSocket webSocket, CancellationToken cancellationToken)
    {
        using var messageBytes = new MemoryStream();
        var readChunk = new byte[4096];
        WebSocketReceiveResult receiveResult;
        do
        {
            receiveResult = await webSocket.ReceiveAsync(new ArraySegment<byte>(readChunk), cancellationToken);
            if (receiveResult.MessageType == WebSocketMessageType.Close)
                return null;
            messageBytes.Write(readChunk, 0, receiveResult.Count);
        } while (!receiveResult.EndOfMessage);

        return Encoding.UTF8.GetString(messageBytes.ToArray());
    }

    private sealed record HandshakeRequest(
        [property: JsonPropertyName("install_key")] string? InstallKey,
        [property: JsonPropertyName("device_name")] string DeviceName);

    private sealed record IdentityResponse(
        [property: JsonPropertyName("device_id")] string? DeviceId,
        [property: JsonPropertyName("install_key")] string? InstallKey);

    private sealed record ConnectionRequestMessage(
        [property: JsonPropertyName("target_device_id")] string TargetDeviceId,
        [property: JsonPropertyName("requesting_computer_name")] string RequestingComputerName)
    {
        [JsonPropertyName("type")]
        public string Type { get; } = "connection-request";
    }

    private sealed record ConnectionActionMessage(
        [property: JsonPropertyName("type")] string Type,
        [property: JsonPropertyName("request_id")] string RequestId);

    private sealed record WebRtcSdpMessage(
        [property: JsonPropertyName("type")] string Type,
        [property: JsonPropertyName("request_id")] string RequestId,
        [property: JsonPropertyName("sdp")] string Sdp);

    private sealed record WebRtcIceCandidateMessage(
        [property: JsonPropertyName("request_id")] string RequestId,
        [property: JsonPropertyName("candidate")] JsonElement Candidate)
    {
        [JsonPropertyName("type")]
        public string Type { get; } = "webrtc-ice-candidate";
    }
}
