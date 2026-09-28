using System.Diagnostics;
using System.Globalization;
using System.Threading.Channels;
using SIPSorcery.Net;

namespace RemoteDesktopClient.WebRTC;

public sealed class RemoteSessionTelemetry
{
    private readonly string _requestId;
    private readonly WebRtcRole _role;
    private readonly string _localDeviceId;
    private readonly string _remoteDeviceId;
    private readonly string _remoteDeviceName;
    private readonly TimeSpan _sampleInterval;
    private readonly DateTimeOffset _sessionStartedAt = DateTimeOffset.Now;

    private long _framesDecodedSinceLastSample;
    private long _framesRenderedSinceLastSample;
    private long _bytesReceivedSinceLastSample;

    private long _framesEncodedSinceLastSample;
    private long _framesSentSinceLastSample;

    private long _totalFramesDecoded;
    private long _totalFramesRendered;
    private long _totalBytesReceived;

    private readonly object _rtcpLock = new();
    private uint? _latestJitterUnits;
    private int? _latestPacketsLost;
    private TimeSpan? _latestRoundTripTime;

    private bool? _lastIsRelayed;
    private string? _lastLocalCandidateType;
    private string? _lastRemoteCandidateType;
    private RTCPeerConnectionState? _lastConnectionState;
    private RTCIceConnectionState? _lastIceConnectionState;
    private string? _lastResolution;
    private string? _lastCodec;

    private RTCPeerConnection? _peerConnection;
    private int _configuredFps;
    private int _configuredBitrateKbps;

    private Channel<string>? _writeChannel;
    private Task? _writerTask;
    private Task? _samplerTask;
    private CancellationTokenSource? _cts;
    private StreamWriter? _writer;
    private bool _loggedWriteFailure;
    private bool _started;
    private bool _stopped;

    public RemoteSessionTelemetry(string requestId, WebRtcRole role, string localDeviceId, string remoteDeviceId, string remoteDeviceName, TimeSpan sampleInterval)
    {
        _requestId = requestId;
        _role = role;
        _localDeviceId = localDeviceId;
        _remoteDeviceId = remoteDeviceId;
        _remoteDeviceName = remoteDeviceName;
        _sampleInterval = sampleInterval;
    }

    public void Start(RTCPeerConnection peerConnection, int configuredFps, int configuredBitrateKbps, string? resolution, string? codec)
    {
        if (_started)
            return;
        _started = true;

        try
        {
            _peerConnection = peerConnection;
            _configuredFps = configuredFps;
            _configuredBitrateKbps = configuredBitrateKbps;

            var path = BuildLogFilePath(_requestId);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            _writer = new StreamWriter(new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read));

            _writeChannel = Channel.CreateUnbounded<string>(new UnboundedChannelOptions { SingleReader = true });
            _cts = new CancellationTokenSource();
            _writerTask = Task.Run(() => RunWriterAsync(_cts.Token));

            peerConnection.OnReceiveReport += OnReceiveReport;
            peerConnection.OnSendReport += OnSendReport;

            _lastResolution = resolution;
            _lastCodec = codec;
            _lastIsRelayed = IceConnectionInspector.IsUsingRelay(peerConnection);
            (_lastLocalCandidateType, _lastRemoteCandidateType) = ReadCandidateTypes(peerConnection);

            Enqueue(FormatSessionStarted(_requestId, _role, _localDeviceId, _remoteDeviceId, _remoteDeviceName, _sessionStartedAt));
            Enqueue(FormatVideoBlock(resolution, configuredFps, configuredBitrateKbps, codec));
            Enqueue(FormatConnectionBlock(_lastIsRelayed.Value, _lastLocalCandidateType, _lastRemoteCandidateType));

            _samplerTask = Task.Run(() => RunSamplerAsync(_cts.Token));
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"RemoteSessionTelemetry: failed to start for {_requestId} - continuing without telemetry. {ex}");
            SafeDisposeWriter();
        }
    }

    public void OnVideoFrameDecoded()
    {
        Interlocked.Increment(ref _framesDecodedSinceLastSample);
        Interlocked.Increment(ref _totalFramesDecoded);
    }

    public void OnVideoBytesReceived(int byteCount)
    {
        Interlocked.Add(ref _bytesReceivedSinceLastSample, byteCount);
        Interlocked.Add(ref _totalBytesReceived, byteCount);
    }

    public void OnVideoFrameRendered()
    {
        Interlocked.Increment(ref _framesRenderedSinceLastSample);
        Interlocked.Increment(ref _totalFramesRendered);
    }

    public void OnVideoFrameEncoded()
    {
        Interlocked.Increment(ref _framesEncodedSinceLastSample);
    }

    public void OnVideoFrameSent()
    {
        Interlocked.Increment(ref _framesSentSinceLastSample);
    }

    public void OnVideoFormatKnown(string resolution, string codec)
    {
        if (!_started || _stopped)
            return;
        try
        {
            if (_lastResolution is null && _lastCodec is null)
            {
                _lastResolution = resolution;
                _lastCodec = codec;
                Enqueue($"VIDEO_STARTED{Environment.NewLine}resolution={resolution}{Environment.NewLine}codec={codec}");
                return;
            }
            if (_lastResolution != resolution)
            {
                Enqueue($"RESOLUTION_CHANGED{Environment.NewLine}previous={_lastResolution}{Environment.NewLine}current={resolution}");
                _lastResolution = resolution;
            }
            if (_lastCodec != codec)
            {
                Enqueue($"CODEC_CHANGED{Environment.NewLine}previous={_lastCodec}{Environment.NewLine}current={codec}");
                _lastCodec = codec;
            }
        }
        catch (Exception ex)
        {
            TraceFailureOnce(ex);
        }
    }

    public void OnConnectionStateChanged(RTCPeerConnectionState state)
    {
        if (!_started || _stopped)
            return;
        try
        {
            if (_lastConnectionState == state)
                return;
            _lastConnectionState = state;
            var eventName = state switch
            {
                RTCPeerConnectionState.connected => "WEBRTC_CONNECTED",
                RTCPeerConnectionState.failed => "WEBRTC_FAILED",
                _ => $"WEBRTC_STATE_CHANGED{Environment.NewLine}state={state}",
            };
            Enqueue(eventName);
        }
        catch (Exception ex)
        {
            TraceFailureOnce(ex);
        }
    }

    public void OnIceConnectionStateChanged(RTCIceConnectionState state)
    {
        if (!_started || _stopped)
            return;
        try
        {
            if (_lastIceConnectionState == state)
                return;
            _lastIceConnectionState = state;
            var eventName = state switch
            {
                RTCIceConnectionState.connected => "ICE_CONNECTED",
                RTCIceConnectionState.disconnected => "ICE_DISCONNECTED",
                RTCIceConnectionState.failed => "ICE_FAILED",
                _ => $"ICE_STATE_CHANGED{Environment.NewLine}state={state}",
            };
            Enqueue(eventName);
        }
        catch (Exception ex)
        {
            TraceFailureOnce(ex);
        }
    }

    public void OnDataChannelOpened()
    {
        if (!_started || _stopped)
            return;
        try { Enqueue("DATACHANNEL_OPENED"); }
        catch (Exception ex) { TraceFailureOnce(ex); }
    }

    public void OnDataChannelClosed()
    {
        if (!_started || _stopped)
            return;
        try { Enqueue("DATACHANNEL_CLOSED"); }
        catch (Exception ex) { TraceFailureOnce(ex); }
    }

    public void OnVideoSettingsAdjusted(int fps, int bitrateKbps)
    {
        if (!_started || _stopped)
            return;
        try { Enqueue(FormatVideoSettingsAdjusted(fps, bitrateKbps)); }
        catch (Exception ex) { TraceFailureOnce(ex); }
    }

    public void Stop(string terminationReason)
    {
        if (!_started || _stopped)
            return;
        _stopped = true;

        try
        {
            if (_peerConnection is { } pc)
            {
                pc.OnReceiveReport -= OnReceiveReport;
                pc.OnSendReport -= OnSendReport;
            }

            var duration = DateTimeOffset.Now - _sessionStartedAt;
            var isRelayed = _peerConnection is { } p ? IceConnectionInspector.IsUsingRelay(p) : (_lastIsRelayed ?? false);
            int? totalPacketsLost;
            lock (_rtcpLock)
                totalPacketsLost = _latestPacketsLost;
            Enqueue(FormatSessionEnded(
                duration,
                terminationReason,
                isRelayed,
                Interlocked.Read(ref _totalFramesDecoded),
                Interlocked.Read(ref _totalFramesRendered),
                Interlocked.Read(ref _totalBytesReceived),
                totalPacketsLost));

            _writeChannel?.Writer.TryComplete();
            _cts?.Cancel();
            _writerTask?.Wait(TimeSpan.FromSeconds(2));
            _samplerTask?.Wait(TimeSpan.FromMilliseconds(500));
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"RemoteSessionTelemetry: error while stopping for {_requestId} (ignored). {ex}");
        }
        finally
        {
            SafeDisposeWriter();
            _cts?.Dispose();
        }
    }

    private async Task RunSamplerAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(_sampleInterval);
        var lastSampleAt = DateTimeOffset.UtcNow;
        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                var now = DateTimeOffset.UtcNow;
                var elapsedSeconds = Math.Max(0.001, (now - lastSampleAt).TotalSeconds);
                lastSampleAt = now;

                var framesDecoded = Interlocked.Exchange(ref _framesDecodedSinceLastSample, 0);
                var framesRendered = Interlocked.Exchange(ref _framesRenderedSinceLastSample, 0);
                var bytesReceived = Interlocked.Exchange(ref _bytesReceivedSinceLastSample, 0);

                var fps = Math.Round(framesDecoded / elapsedSeconds, 1);
                var bitrateKbps = (int)Math.Round(bytesReceived * 8 / elapsedSeconds / 1000);
                var framesDropped = Math.Max(0, framesDecoded - framesRendered);

                var framesEncoded = Interlocked.Exchange(ref _framesEncodedSinceLastSample, 0);
                var framesSent = Interlocked.Exchange(ref _framesSentSinceLastSample, 0);
                var encodedFps = Math.Round(framesEncoded / elapsedSeconds, 1);
                var sentFps = Math.Round(framesSent / elapsedSeconds, 1);

                uint? jitterUnits;
                int? packetsLost;
                TimeSpan? rtt;
                lock (_rtcpLock)
                {
                    jitterUnits = _latestJitterUnits;
                    packetsLost = _latestPacketsLost;
                    rtt = _latestRoundTripTime;
                }

                if (_peerConnection is { } pc)
                {
                    var isRelayed = IceConnectionInspector.IsUsingRelay(pc);
                    var (localType, remoteType) = ReadCandidateTypes(pc);
                    if (_lastIsRelayed is { } previousRelayed && previousRelayed != isRelayed)
                        Enqueue(FormatConnectionChanged(previousRelayed, isRelayed));
                    _lastIsRelayed = isRelayed;
                    _lastLocalCandidateType = localType;
                    _lastRemoteCandidateType = remoteType;

                    Enqueue(FormatMetrics(now, _role, rtt, fps, bitrateKbps, jitterUnits, packetsLost, framesDecoded, framesRendered, framesDropped, bytesReceived));

                    if (_role == WebRtcRole.Offerer)
                        Enqueue(FormatPipelineMetrics(now, encodedFps, sentFps));
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            TraceFailureOnce(ex);
        }
    }

    private async Task RunWriterAsync(CancellationToken cancellationToken)
    {
        if (_writeChannel is null || _writer is null)
            return;
        try
        {
            await foreach (var entry in _writeChannel.Reader.ReadAllAsync(cancellationToken))
            {
                try
                {
                    await _writer.WriteAsync(entry);
                    await _writer.WriteAsync(Environment.NewLine);
                    await _writer.WriteAsync(Environment.NewLine);
                    await _writer.FlushAsync();
                    _loggedWriteFailure = false;
                }
                catch (Exception ex)
                {
                    TraceFailureOnce(ex);
                }
            }
        }
        catch (OperationCanceledException)
        {
            try
            {
                while (_writeChannel.Reader.TryRead(out var entry))
                {
                    await _writer.WriteAsync(entry);
                    await _writer.WriteAsync(Environment.NewLine);
                    await _writer.WriteAsync(Environment.NewLine);
                }
                await _writer.FlushAsync();
            }
            catch (Exception ex)
            {
                TraceFailureOnce(ex);
            }
        }
    }

    private void OnReceiveReport(System.Net.IPEndPoint remoteEndPoint, SDPMediaTypesEnum mediaType, RTCPCompoundPacket report) => RecordCompoundPacket(mediaType, report);

    private void OnSendReport(SDPMediaTypesEnum mediaType, RTCPCompoundPacket report) => RecordCompoundPacket(mediaType, report);

    private void RecordCompoundPacket(SDPMediaTypesEnum mediaType, RTCPCompoundPacket report)
    {
        if (mediaType != SDPMediaTypesEnum.video)
            return;
        try
        {
            var sample = report.SenderReport?.ReceptionReports?.FirstOrDefault()
                ?? report.ReceiverReport?.ReceptionReports?.FirstOrDefault();
            if (sample is null)
                return;

            var rtt = RtcpRoundTripCalculator.ComputeRoundTripTime(sample.LastSenderReportTimestamp, sample.DelaySinceLastSenderReport, DateTime.UtcNow);
            lock (_rtcpLock)
            {
                _latestJitterUnits = sample.Jitter;
                _latestPacketsLost = sample.PacketsLost;
                if (rtt is { } value)
                    _latestRoundTripTime = value;
            }
        }
        catch (Exception ex)
        {
            TraceFailureOnce(ex);
        }
    }

    private static (string? LocalType, string? RemoteType) ReadCandidateTypes(RTCPeerConnection pc)
    {
        var nominated = pc.GetRtpChannel()?.NominatedEntry;
        if (nominated is null)
            return (null, null);
        return (nominated.LocalCandidate?.type.ToString(), nominated.RemoteCandidate?.type.ToString());
    }

    private void Enqueue(string block)
    {
        if (_writeChannel is null)
            return;
        _writeChannel.Writer.TryWrite(block);
    }

    private void TraceFailureOnce(Exception ex)
    {
        if (_loggedWriteFailure)
            return;
        _loggedWriteFailure = true;
        Trace.WriteLine($"RemoteSessionTelemetry: error for {_requestId} (further errors this session are suppressed). {ex}");
    }

    private void SafeDisposeWriter()
    {
        try { _writer?.Flush(); } catch { }
        try { _writer?.Dispose(); } catch { }
        _writer = null;
    }

    internal static string FormatSessionStarted(string requestId, WebRtcRole role, string localDeviceId, string remoteDeviceId, string remoteDeviceName, DateTimeOffset startedAt) => string.Join(Environment.NewLine,
        "SESSION_STARTED",
        $"request_id={requestId}",
        $"role={(role == WebRtcRole.Offerer ? "TARGET" : "REQUESTER")}",
        $"local_device_id={localDeviceId}",
        $"remote_device_id={remoteDeviceId}",
        $"remote_device_name={remoteDeviceName}",
        $"started_at={startedAt:O}");

    internal static string FormatVideoBlock(string? resolution, int configuredFps, int configuredBitrateKbps, string? codec) => string.Join(Environment.NewLine,
        "VIDEO",
        $"resolution={resolution ?? "pending"}",
        $"configured_fps={configuredFps}",
        $"configured_bitrate_kbps={configuredBitrateKbps}",
        $"codec={codec ?? "pending"}");

    internal static string FormatConnectionBlock(bool isRelayed, string? localCandidateType, string? remoteCandidateType)
    {
        var lines = new List<string> { "CONNECTION", $"type={(isRelayed ? "TURN" : "DIRECT")}" };
        if (localCandidateType is not null)
            lines.Add($"local_candidate_type={localCandidateType}");
        if (remoteCandidateType is not null)
            lines.Add($"remote_candidate_type={remoteCandidateType}");
        return string.Join(Environment.NewLine, lines);
    }

    internal static string FormatConnectionChanged(bool previousIsRelayed, bool currentIsRelayed) => string.Join(Environment.NewLine,
        "CONNECTION_CHANGED",
        $"previous={(previousIsRelayed ? "TURN" : "DIRECT")}",
        $"current={(currentIsRelayed ? "TURN" : "DIRECT")}");

    internal static string FormatVideoSettingsAdjusted(int fps, int bitrateKbps) => string.Join(Environment.NewLine,
        "VIDEO_SETTINGS_ADJUSTED",
        $"fps={fps}",
        $"bitrate_kbps={bitrateKbps}");

    internal static string FormatPipelineMetrics(DateTimeOffset timestamp, double encodedFps, double sentFps) => string.Join(Environment.NewLine,
        "PIPELINE_METRICS",
        $"timestamp={timestamp:O}",
        $"encoded_fps={encodedFps.ToString("F1", CultureInfo.InvariantCulture)}",
        $"sent_fps={sentFps.ToString("F1", CultureInfo.InvariantCulture)}");

    internal static string FormatMetrics(DateTimeOffset timestamp, WebRtcRole role, TimeSpan? rtt, double fps, int bitrateKbps, uint? jitterUnits, int? packetsLost, long framesDecoded, long framesRendered, long framesDropped, long bytesReceived)
    {
        var lines = new List<string> { "METRICS", $"timestamp={timestamp:O}", $"user={(role == WebRtcRole.Offerer ? "TARGET" : "REQUESTER")}" };
        if (rtt is { } value)
        {
            lines.Add($"latency_ms={value.TotalMilliseconds.ToString("F0", CultureInfo.InvariantCulture)}");
            lines.Add("latency_source=rtcp_sr_dlsr");
        }
        else
        {
            lines.Add("latency_ms=");
            lines.Add("latency_source=unavailable");
        }
        lines.Add($"fps={fps.ToString("F1", CultureInfo.InvariantCulture)}");
        lines.Add($"bitrate_kbps={bitrateKbps}");
        lines.Add(jitterUnits is { } jitter ? $"jitter_ms={(jitter / 90.0).ToString("F1", CultureInfo.InvariantCulture)}" : "jitter_ms=");
        lines.Add(packetsLost is { } lost ? $"packets_lost={lost}" : "packets_lost=");
        lines.Add($"frames_decoded={framesDecoded}");
        lines.Add($"frames_rendered={framesRendered}");
        lines.Add($"frames_dropped={framesDropped}");
        lines.Add($"bytes_received={bytesReceived}");
        return string.Join(Environment.NewLine, lines);
    }

    internal static string FormatSessionEnded(TimeSpan duration, string terminationReason, bool finalIsRelayed, long totalFramesDecoded, long totalFramesRendered, long totalBytesReceived, int? totalPacketsLost) => string.Join(Environment.NewLine,
        "SESSION_ENDED",
        $"duration_sec={(int)duration.TotalSeconds}",
        $"termination_reason={terminationReason}",
        $"final_connection_type={(finalIsRelayed ? "TURN" : "DIRECT")}",
        $"total_frames_decoded={totalFramesDecoded}",
        $"total_frames_rendered={totalFramesRendered}",
        $"total_frames_dropped={Math.Max(0, totalFramesDecoded - totalFramesRendered)}",
        $"total_bytes_received={totalBytesReceived}",
        $"total_packets_lost={(totalPacketsLost is { } lost ? lost.ToString(CultureInfo.InvariantCulture) : "unavailable")}");

    internal static string SanitizeRequestId(string requestId)
    {
        var sanitized = new string(requestId.Where(c => char.IsLetterOrDigit(c) || c is '-' or '_').ToArray());
        return string.IsNullOrEmpty(sanitized) ? $"unknown-session-{Guid.NewGuid():N}" : sanitized;
    }

    internal static string BuildLogFilePath(string requestId)
    {
        var directory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "RemoteConnect", "logs", "remote-sessions");
        return Path.Combine(directory, $"{SanitizeRequestId(requestId)}.txt");
    }
}
