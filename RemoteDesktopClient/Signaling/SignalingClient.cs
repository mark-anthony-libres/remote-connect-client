using RemoteDesktopClient.Services;

namespace RemoteDesktopClient.Signaling;

public sealed class SignalingClient
{
    private readonly DeviceIdentityClient _deviceIdentityClient;

    public event Action<string, string, string>? OfferReceived;

    public event Action<string, string, string>? AnswerReceived;

    public event Action<string, string, string>? CandidateReceived;

    public SignalingClient(DeviceIdentityClient deviceIdentityClient)
    {
        _deviceIdentityClient = deviceIdentityClient;
        _deviceIdentityClient.WebRtcOfferReceived += (requestId, fromDeviceId, sdp) => OfferReceived?.Invoke(requestId, fromDeviceId, sdp);
        _deviceIdentityClient.WebRtcAnswerReceived += (requestId, fromDeviceId, sdp) => AnswerReceived?.Invoke(requestId, fromDeviceId, sdp);
        _deviceIdentityClient.WebRtcIceCandidateReceived += (requestId, fromDeviceId, candidateJson) => CandidateReceived?.Invoke(requestId, fromDeviceId, candidateJson);
    }

    public Task SendOfferAsync(string requestId, string sdp, CancellationToken cancellationToken = default) =>
        _deviceIdentityClient.SendWebRtcOfferAsync(requestId, sdp, cancellationToken);

    public Task SendAnswerAsync(string requestId, string sdp, CancellationToken cancellationToken = default) =>
        _deviceIdentityClient.SendWebRtcAnswerAsync(requestId, sdp, cancellationToken);

    public Task SendCandidateAsync(string requestId, string candidateJson, CancellationToken cancellationToken = default) =>
        _deviceIdentityClient.SendWebRtcIceCandidateAsync(requestId, candidateJson, cancellationToken);
}
