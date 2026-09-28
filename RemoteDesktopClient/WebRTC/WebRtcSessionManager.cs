using System.Diagnostics;
using System.Text;
using RemoteDesktopClient.Core.Configuration;
using RemoteDesktopClient.Core.Control;
using RemoteDesktopClient.Signaling;
using SIPSorcery.Net;
using SIPSorceryMedia.Abstractions;
using SIPSorceryMedia.FFmpeg;

namespace RemoteDesktopClient.WebRTC;

public enum WebRtcRole
{
    Offerer,
    Answerer,
}

public enum WebRtcStage
{
    OfferSent,
    OfferReceived,
    AnswerSent,
    AnswerReceived,
    IceConnected,
    DataChannelConnected,
}

public readonly record struct RemoteVideoDebugStats(bool HasVideo, int BitrateKbps, double Fps, bool IsRelayed);

public sealed class WebRtcSessionManager : IDisposable
{
    private const string DataChannelLabel = "remoteconnect";

    private readonly SignalingClient _signalingClient;
    private readonly string _requestId;
    private readonly WebRtcRole _role;
    private readonly AppSettings _settings;

    private readonly Action<string, string, string> _offerHandler;
    private readonly Action<string, string, string> _answerHandler;
    private readonly Action<string, string, string> _candidateHandler;

    private RTCPeerConnection? _peerConnection;
    private RTCDataChannel? _dataChannel;
    private bool _dataChannelOpen;

    private FFmpegScreenSource? _screenSource;
    private FFmpegVideoEndPoint? _videoEndPoint;

    private int _baseVideoBitrateKbps;

    private SIPSorceryMedia.FFmpeg.Monitor? _captureMonitor;
    private VideoFormat? _negotiatedVideoFormat;
    private int _constructedScreenCaptureFps;

    private long _debugBytes;
    private int _debugFrameCount;
    private long _debugLastSampleTicks = DateTime.UtcNow.Ticks;

    private RemoteSessionTelemetry? _telemetry;

    public bool IsDataChannelOpen => _dataChannelOpen;

    public bool IsUsingRelay => _peerConnection is { } pc && IceConnectionInspector.IsUsingRelay(pc);

    public ulong DataChannelBufferedAmount => _dataChannel?.bufferedAmount ?? 0;

    public event Action? Closed;
    private bool _closedRaised;

    public event Action? DataChannelOpened;

    public event Action<string>? MessageReceived;

    public event Action<ControlMessage>? ControlMessageReceived;

    public event Action<string>? Failed;

    public event Action<WebRtcStage>? StageChanged;

    public event Action<byte[], uint, uint, int, VideoPixelFormatsEnum>? VideoFrameReceived;

    public WebRtcSessionManager(SignalingClient signalingClient, string requestId, WebRtcRole role, AppSettings settings)
    {
        _signalingClient = signalingClient;
        _requestId = requestId;
        _role = role;
        _settings = settings;

        _offerHandler = (reqId, fromDeviceId, sdp) =>
        {
            if (reqId == _requestId)
                _ = HandleRemoteOfferAsync(sdp);
        };
        _answerHandler = (reqId, _, sdp) =>
        {
            if (reqId == _requestId)
                HandleRemoteAnswer(sdp);
        };
        _candidateHandler = (reqId, _, candidateJson) =>
        {
            if (reqId == _requestId)
                HandleRemoteCandidate(candidateJson);
        };

        _signalingClient.OfferReceived += _offerHandler;
        _signalingClient.AnswerReceived += _answerHandler;
        _signalingClient.CandidateReceived += _candidateHandler;
    }

    public async Task StartAsync()
    {
        _peerConnection = CreatePeerConnection();
        TrySetupVideo(_peerConnection);

        if (_role == WebRtcRole.Offerer)
        {
            var channel = await _peerConnection.createDataChannel(DataChannelLabel, null);
            WireDataChannel(channel);

            var offer = _peerConnection.createOffer(null);
            await _peerConnection.setLocalDescription(offer);
            await _signalingClient.SendOfferAsync(_requestId, offer.sdp);
            StageChanged?.Invoke(WebRtcStage.OfferSent);
        }
    }

    private void TrySetupVideo(RTCPeerConnection pc)
    {
        try
        {
            if (_role == WebRtcRole.Offerer)
            {
                var monitors = FFmpegMonitorManager.GetMonitorDevices();
                var monitor = monitors?.Find(m => m.Primary) ?? (monitors?.Count > 0 ? monitors[0] : null);
                if (monitor is null)
                {
                    Trace.WriteLine("[WebRTC] No monitor found for screen capture - continuing without video.");
                    return;
                }
                _captureMonitor = monitor;

                _constructedScreenCaptureFps = _settings.ScreenCaptureFps;
                var screenSource = new FFmpegScreenSource(monitor.Path, monitor.Rect, _constructedScreenCaptureFps);
                WireScreenSource(screenSource, pc);
                _baseVideoBitrateKbps = VideoBitrateCalculator.CalculateKbps(monitor.Rect.Width, monitor.Rect.Height, _constructedScreenCaptureFps);
                _screenSource = screenSource;

                var videoTrack = new MediaStreamTrack(screenSource.GetVideoSourceFormats(), MediaStreamStatusEnum.SendOnly);
                pc.addTrack(videoTrack);
                pc.OnVideoFormatsNegotiated += formats =>
                {
                    Trace.WriteLine($"[WebRTC] Offerer video format negotiated: {string.Join(", ", formats.Select(f => f.Codec))}");
                    _negotiatedVideoFormat = formats.First();
                    screenSource.SetVideoSourceFormat(_negotiatedVideoFormat.Value);
                    _telemetry?.OnVideoFormatKnown($"{monitor.Rect.Width}x{monitor.Rect.Height}", _negotiatedVideoFormat.Value.Codec.ToString());
                };

                Trace.WriteLine($"[WebRTC] Screen capture ready: {monitor.Path}, {monitor.Rect}, {_constructedScreenCaptureFps}fps (direct-connection rate; final fps decided once connection type is known), base bitrate {_baseVideoBitrateKbps}kbps (final bitrate decided once connection type is known)");
            }
            else
            {
                var videoEndPoint = new FFmpegVideoEndPoint();
                videoEndPoint.OnVideoSourceError += err => Trace.WriteLine($"[WebRTC] Video sink error ({_role}): {err}");
                var decodedFrameCount = 0;
                string? negotiatedCodec = null;
                var reportedVideoFormat = false;
                videoEndPoint.OnVideoSinkDecodedSampleFaster += rawImage =>
                {
                    decodedFrameCount++;
                    Interlocked.Increment(ref _debugFrameCount);
                    _telemetry?.OnVideoFrameDecoded();
                    if (!reportedVideoFormat && _telemetry is { } telemetry)
                    {
                        reportedVideoFormat = true;
                        telemetry.OnVideoFormatKnown($"{rawImage.Width}x{rawImage.Height}", negotiatedCodec ?? "unknown");
                    }
                    if (decodedFrameCount == 1 || decodedFrameCount % 30 == 0)
                        Trace.WriteLine($"[WebRTC] Answerer decoded frame #{decodedFrameCount}: {rawImage.Width}x{rawImage.Height}, stride {rawImage.Stride}, format {rawImage.PixelFormat}.");
                    VideoFrameReceived?.Invoke(rawImage.GetBuffer(), (uint)rawImage.Width, (uint)rawImage.Height, rawImage.Stride, rawImage.PixelFormat);
                };
                _videoEndPoint = videoEndPoint;
                pc.OnRtpPacketReceived += (_, mediaType, rtpPacket) =>
                {
                    if (mediaType == SDPMediaTypesEnum.video)
                    {
                        Interlocked.Add(ref _debugBytes, rtpPacket.Payload.Length);
                        _telemetry?.OnVideoBytesReceived(rtpPacket.Payload.Length);
                    }
                };

                var videoTrack = new MediaStreamTrack(videoEndPoint.GetVideoSourceFormats(), MediaStreamStatusEnum.RecvOnly);
                pc.addTrack(videoTrack);
                pc.OnVideoFormatsNegotiated += formats =>
                {
                    Trace.WriteLine($"[WebRTC] Answerer video format negotiated: {string.Join(", ", formats.Select(f => f.Codec))}");
                    negotiatedCodec = formats.First().Codec.ToString();
                    videoEndPoint.SetVideoSourceFormat(formats.First());
                };

                pc.OnVideoFrameReceived += videoEndPoint.GotVideoFrame;

                Trace.WriteLine("[WebRTC] Video receive endpoint ready");
            }
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[WebRTC] Video setup failed ({_role}) - continuing without video (DataChannel unaffected). {ex}");
            _screenSource = null;
            _videoEndPoint = null;
        }
    }

    private void WireScreenSource(FFmpegScreenSource screenSource, RTCPeerConnection pc)
    {
        screenSource.OnVideoSourceError += err => Trace.WriteLine($"[WebRTC] Screen source error ({_role}): {err}");
        screenSource.OnVideoSourceEncodedSample += (duration, sample) =>
        {
            _telemetry?.OnVideoFrameEncoded();
            pc.SendVideo(duration, sample);
            _telemetry?.OnVideoFrameSent();
        };
        var encodedFrameCount = 0;
        screenSource.OnVideoSourceEncodedSample += (duration, sample) =>
        {
            encodedFrameCount++;
            if (encodedFrameCount == 1 || encodedFrameCount % 30 == 0)
                Trace.WriteLine($"[WebRTC] Offerer encoded frame #{encodedFrameCount}: {sample.Length} bytes, duration {duration}.");
        };
    }

    private async Task StartVideoIfAvailableAsync()
    {
        if (_screenSource is not { } screenSource)
            return;
        try
        {
            if (_peerConnection is { } pc)
            {
                var isRelayed = IceConnectionInspector.IsUsingRelay(pc);

                var finalBitrateKbps = VideoBitrateCalculator.ApplyMaxBitrate(_baseVideoBitrateKbps, _settings.MaxVideoBitrateKbps);
                screenSource.SetVideoEncoderBitrate(avgBitrate: (long)finalBitrateKbps * 1000);
                Trace.WriteLine($"[WebRTC] Video bitrate ({_role}): {finalBitrateKbps}kbps ({(isRelayed ? "TURN/relay" : "direct")} connection, base {_baseVideoBitrateKbps}kbps, max {_settings.MaxVideoBitrateKbps}kbps).");

                var finalFps = ScreenCaptureFpsCalculator.Calculate(_settings.ScreenCaptureFps, isRelayed, _settings.MaxScreenCaptureFps);
                if (finalFps != _constructedScreenCaptureFps && _captureMonitor is { } monitor)
                {
                    var oldScreenSource = screenSource;
                    var newScreenSource = new FFmpegScreenSource(monitor.Path, monitor.Rect, finalFps);
                    WireScreenSource(newScreenSource, pc);
                    if (_negotiatedVideoFormat is { } negotiatedFormat)
                        newScreenSource.SetVideoSourceFormat(negotiatedFormat);

                    screenSource = newScreenSource;
                    _screenSource = newScreenSource;
                    _constructedScreenCaptureFps = finalFps;
                    _ = oldScreenSource.CloseVideo();

                    Trace.WriteLine($"[WebRTC] Screen capture fps adjusted ({_role}): {finalFps}fps ({(isRelayed ? "TURN/relay" : "direct")} connection, configured {_settings.ScreenCaptureFps}fps, max-when-relayed {_settings.MaxScreenCaptureFps}fps) - source reconstructed since FFmpeg's capture rate is fixed at construction.");
                }
            }

            await screenSource.StartVideo();
            Trace.WriteLine($"[WebRTC] Screen capture started ({_role})");
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[WebRTC] Failed to start screen capture ({_role}). {ex}");
        }
    }

    public async Task ApplyLiveVideoSettingsAsync(int fps, int bitrateKbps)
    {
        if (_role != WebRtcRole.Offerer)
            return;
        if (!_settings.ShowRemoteDebug)
        {
            Trace.WriteLine($"[WebRTC] ApplyLiveVideoSettingsAsync skipped ({_role}) - this side's own SHOW_REMOTE_DEBUG is off.");
            return;
        }
        if (_screenSource is not { } screenSource || _captureMonitor is not { } monitor || _peerConnection is not { } pc)
        {
            Trace.WriteLine($"[WebRTC] ApplyLiveVideoSettingsAsync skipped ({_role}) - no active capture source for this session.");
            return;
        }

        try
        {
            await screenSource.CloseVideo();

            var newScreenSource = new FFmpegScreenSource(monitor.Path, monitor.Rect, fps);
            WireScreenSource(newScreenSource, pc);
            if (_negotiatedVideoFormat is { } negotiatedFormat)
                newScreenSource.SetVideoSourceFormat(negotiatedFormat);
            newScreenSource.SetVideoEncoderBitrate(avgBitrate: (long)bitrateKbps * 1000);

            _screenSource = newScreenSource;
            _constructedScreenCaptureFps = fps;
            await newScreenSource.StartVideo();

            _telemetry?.OnVideoSettingsAdjusted(fps, bitrateKbps);
            Trace.WriteLine($"[WebRTC] Live video settings applied ({_role}): {fps}fps, {bitrateKbps}kbps (manual debug override).");
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[WebRTC] ApplyLiveVideoSettingsAsync failed ({_role}). {ex}");
        }
    }

    private List<RTCIceServer> BuildIceServers()
    {
        var iceServers = new List<RTCIceServer>();

        if (string.IsNullOrWhiteSpace(_settings.StunServerUrl))
        {
            Trace.WriteLine("[WebRTC] No STUN server configured");
        }
        else
        {
            Trace.WriteLine($"[WebRTC] STUN server configured: {_settings.StunServerUrl}");
            iceServers.Add(new RTCIceServer { urls = _settings.StunServerUrl });
        }

        if (string.IsNullOrWhiteSpace(_settings.TurnServerUrl))
        {
            Trace.WriteLine("[WebRTC] No TURN server configured");
        }
        else
        {
            Trace.WriteLine($"[WebRTC] TURN server configured: {_settings.TurnServerUrl}");
            var turnServer = new RTCIceServer { urls = _settings.TurnServerUrl };
            if (!string.IsNullOrWhiteSpace(_settings.TurnUsername))
                turnServer.username = _settings.TurnUsername;
            if (!string.IsNullOrWhiteSpace(_settings.TurnCredential))
                turnServer.credential = _settings.TurnCredential;
            iceServers.Add(turnServer);
        }

        return iceServers;
    }

    private RTCPeerConnection CreatePeerConnection()
    {
        var config = new RTCConfiguration { iceServers = BuildIceServers() };
        var pc = new RTCPeerConnection(config);

        pc.onicecandidate += candidate =>
        {
            if (candidate is null)
                return;
            Trace.WriteLine($"[WebRTC] Local ICE candidate ({_role}): {candidate.candidate}");
            _ = _signalingClient.SendCandidateAsync(_requestId, candidate.toJSON());
        };

        pc.onconnectionstatechange += state =>
        {
            _telemetry?.OnConnectionStateChanged(state);
            if (state == RTCPeerConnectionState.failed)
                Failed?.Invoke($"Peer connection state = {state}");
            else if (state == RTCPeerConnectionState.connected)
                _ = StartVideoIfAvailableAsync();
        };

        pc.oniceconnectionstatechange += state =>
        {
            _telemetry?.OnIceConnectionStateChanged(state);
            if (state == RTCIceConnectionState.connected)
                StageChanged?.Invoke(WebRtcStage.IceConnected);
        };

        pc.ondatachannel += channel =>
        {
            Trace.WriteLine($"[WebRTC] ondatachannel fired ({_role}): received channel '{channel.label}', readyState={channel.readyState}");
            WireDataChannel(channel);
        };

        return pc;
    }

    private void WireDataChannel(RTCDataChannel channel)
    {
        _dataChannel = channel;
        Trace.WriteLine($"[WebRTC] WireDataChannel ({_role}): initial readyState={channel.readyState}");

        channel.onopen += () =>
        {
            Trace.WriteLine($"[WebRTC] DataChannel onopen fired ({_role})");
            MarkDataChannelOpen();
        };
        channel.onclose += () =>
        {
            Trace.WriteLine($"[WebRTC] DataChannel onclose fired ({_role})");
            _dataChannelOpen = false;
            _telemetry?.OnDataChannelClosed();
        };
        channel.onmessage += (_, type, data) =>
        {
            if (type != DataChannelPayloadProtocols.WebRTC_String)
            {
                Failed?.Invoke($"Received a non-text DataChannel payload (type {type}); this class only handles text.");
                return;
            }

            var text = Encoding.UTF8.GetString(data);
            if (ControlMessageSerializer.TryDeserialize(text) is { } controlMessage)
                ControlMessageReceived?.Invoke(controlMessage);
            else
                MessageReceived?.Invoke(text);
        };

        if (channel.readyState == RTCDataChannelState.open)
            MarkDataChannelOpen();
    }

    private void MarkDataChannelOpen()
    {
        if (_dataChannelOpen)
            return;
        _dataChannelOpen = true;
        StageChanged?.Invoke(WebRtcStage.DataChannelConnected);
        DataChannelOpened?.Invoke();
        _telemetry?.OnDataChannelOpened();
    }

    private async Task HandleRemoteOfferAsync(string sdp)
    {
        if (_peerConnection is null)
        {
            Failed?.Invoke("Received a WebRTC offer before this session's peer connection existed.");
            return;
        }

        var offerInit = new RTCSessionDescriptionInit { type = RTCSdpType.offer, sdp = sdp };
        var result = _peerConnection.setRemoteDescription(offerInit);
        if (result != SetDescriptionResultEnum.OK)
        {
            Failed?.Invoke($"setRemoteDescription(offer) returned {result}.");
            return;
        }
        StageChanged?.Invoke(WebRtcStage.OfferReceived);

        if (_peerConnection.signalingState != RTCSignalingState.have_remote_offer)
            return;

        var answer = _peerConnection.createAnswer(null);
        await _peerConnection.setLocalDescription(answer);
        await _signalingClient.SendAnswerAsync(_requestId, answer.sdp);
        StageChanged?.Invoke(WebRtcStage.AnswerSent);
    }

    private void HandleRemoteAnswer(string sdp)
    {
        if (_peerConnection is null)
        {
            Failed?.Invoke("Received a WebRTC answer before this session's peer connection existed.");
            return;
        }

        var answerInit = new RTCSessionDescriptionInit { type = RTCSdpType.answer, sdp = sdp };
        var result = _peerConnection.setRemoteDescription(answerInit);
        if (result != SetDescriptionResultEnum.OK)
        {
            Failed?.Invoke($"setRemoteDescription(answer) returned {result}.");
            return;
        }
        StageChanged?.Invoke(WebRtcStage.AnswerReceived);
    }

    private void HandleRemoteCandidate(string candidateJson)
    {
        if (_peerConnection is null)
            return;

        if (!RTCIceCandidateInit.TryParse(candidateJson, out var candidateInit))
        {
            Failed?.Invoke("Received a WebRTC ICE candidate that could not be parsed.");
            return;
        }
        _peerConnection.addIceCandidate(candidateInit);
    }

    public void SendText(string text)
    {
        if (_dataChannel is null || !_dataChannelOpen)
            throw new InvalidOperationException("Cannot send: the WebRTC DataChannel is not open.");
        _dataChannel.send(text);
    }

    public void SendControlMessage(ControlMessage message) => SendText(ControlMessageSerializer.Serialize(message));

    public RemoteVideoDebugStats SampleVideoStats()
    {
        if (_videoEndPoint is null || _peerConnection is not { } pc)
            return new RemoteVideoDebugStats(HasVideo: false, BitrateKbps: 0, Fps: 0, IsRelayed: false);

        var nowTicks = DateTimeOffset.UtcNow.Ticks;
        var lastTicks = Interlocked.Exchange(ref _debugLastSampleTicks, nowTicks);
        var elapsedSeconds = Math.Max(0.001, (nowTicks - lastTicks) / (double)TimeSpan.TicksPerSecond);

        var bytes = Interlocked.Exchange(ref _debugBytes, 0);
        var frames = Interlocked.Exchange(ref _debugFrameCount, 0);

        var bitrateKbps = (int)Math.Round(bytes * 8 / elapsedSeconds / 1000);
        var fps = Math.Round(frames / elapsedSeconds, 1);
        return new RemoteVideoDebugStats(HasVideo: true, bitrateKbps, fps, IceConnectionInspector.IsUsingRelay(pc));
    }

    public void RecordFrameRendered() => _telemetry?.OnVideoFrameRendered();

    public void StartTelemetry(string localDeviceId, string remoteDeviceId, string remoteDeviceName)
    {
        if (_peerConnection is not { } pc)
            return;

        _telemetry = new RemoteSessionTelemetry(_requestId, _role, localDeviceId, remoteDeviceId, remoteDeviceName, TimeSpan.FromSeconds(_settings.TelemetrySampleIntervalSeconds));
        _telemetry.Start(pc, _settings.ScreenCaptureFps, _settings.MaxVideoBitrateKbps, resolution: null, codec: null);

        if (_role == WebRtcRole.Offerer && _captureMonitor is { } monitor && _negotiatedVideoFormat is { } format)
            _telemetry.OnVideoFormatKnown($"{monitor.Rect.Width}x{monitor.Rect.Height}", format.Codec.ToString());
    }

    public void FlushTelemetryForExport(string terminationReason) => _telemetry?.Stop(terminationReason);

    public void Close(string terminationReason = "normal")
    {
        _telemetry?.Stop(terminationReason);

        _signalingClient.OfferReceived -= _offerHandler;
        _signalingClient.AnswerReceived -= _answerHandler;
        _signalingClient.CandidateReceived -= _candidateHandler;

        if (_screenSource is { } screenSource)
        {
            _ = screenSource.CloseVideo();
            _screenSource = null;
        }
        if (_videoEndPoint is { } videoEndPoint)
        {
            _ = videoEndPoint.CloseVideoSink();
            _ = videoEndPoint.CloseVideo();
            videoEndPoint.Dispose();
            _videoEndPoint = null;
        }

        _peerConnection?.Close("WebRtcSessionManager closed");
        _peerConnection = null;
        _dataChannel = null;
        _dataChannelOpen = false;

        if (!_closedRaised)
        {
            _closedRaised = true;
            Closed?.Invoke();
        }
    }

    public void Dispose() => Close();
}
