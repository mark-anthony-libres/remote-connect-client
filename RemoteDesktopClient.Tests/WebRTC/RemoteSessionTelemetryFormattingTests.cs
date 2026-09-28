using RemoteDesktopClient.WebRTC;

namespace RemoteDesktopClient.Tests.WebRTC;

public class RemoteSessionTelemetryFormattingTests
{
    [Fact]
    public void SanitizeRequestId_KeepsAPlainUuidUnchanged()
    {
        var result = RemoteSessionTelemetry.SanitizeRequestId("3fa85f64-5717-4562-b3fc-2c963f66afa6");
        Assert.Equal("3fa85f64-5717-4562-b3fc-2c963f66afa6", result);
    }

    [Theory]
    [InlineData("../../etc/passwd")]
    [InlineData("..\\..\\windows\\system32\\evil")]
    [InlineData("abc/def")]
    [InlineData("abc\\def")]
    [InlineData("a b c")]
    [InlineData("request:id?")]
    public void SanitizeRequestId_StripsPathSeparatorsAndOtherUnsafeCharacters(string maliciousId)
    {
        var result = RemoteSessionTelemetry.SanitizeRequestId(maliciousId);

        Assert.DoesNotContain('/', result);
        Assert.DoesNotContain('\\', result);
        Assert.DoesNotContain(':', result);
        Assert.DoesNotContain(' ', result);
        Assert.DoesNotContain('?', result);
        Assert.DoesNotContain(".", result.Replace("-", ""));
    }

    [Fact]
    public void SanitizeRequestId_NeverProducesAnEmptyFilename()
    {
        var result = RemoteSessionTelemetry.SanitizeRequestId("../../../");
        Assert.False(string.IsNullOrWhiteSpace(result));
    }

    [Fact]
    public void BuildLogFilePath_UsesTheSanitizedIdAsTheFilename()
    {
        var path = RemoteSessionTelemetry.BuildLogFilePath("abc/def");

        Assert.EndsWith("abcdef.txt", path);
        Assert.Contains(Path.Combine("RemoteConnect", "logs", "remote-sessions"), path);
        Assert.StartsWith(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), path);
    }

    [Fact]
    public void BuildLogFilePath_DifferentRequestIds_ProduceDifferentPaths()
    {
        var pathA = RemoteSessionTelemetry.BuildLogFilePath("request-a");
        var pathB = RemoteSessionTelemetry.BuildLogFilePath("request-b");

        Assert.NotEqual(pathA, pathB);
    }

    [Fact]
    public void FormatSessionStarted_ContainsEveryRequiredField()
    {
        var startedAt = new DateTimeOffset(2026, 9, 24, 21, 0, 0, TimeSpan.FromHours(8));
        var text = RemoteSessionTelemetry.FormatSessionStarted("req-1", WebRtcRole.Answerer, "1112223333", "4445556666", "Office-PC", startedAt);

        Assert.StartsWith("SESSION_STARTED", text);
        Assert.Contains("request_id=req-1", text);
        Assert.Contains("role=REQUESTER", text);
        Assert.Contains("local_device_id=1112223333", text);
        Assert.Contains("remote_device_id=4445556666", text);
        Assert.Contains("remote_device_name=Office-PC", text);
    }

    [Fact]
    public void FormatSessionStarted_MapsOffererRoleToTarget()
    {
        var text = RemoteSessionTelemetry.FormatSessionStarted("req-1", WebRtcRole.Offerer, "a", "b", "c", DateTimeOffset.Now);
        Assert.Contains("role=TARGET", text);
    }

    [Fact]
    public void FormatVideoBlock_UsesPendingPlaceholder_WhenResolutionOrCodecUnknown()
    {
        var text = RemoteSessionTelemetry.FormatVideoBlock(resolution: null, configuredFps: 60, configuredBitrateKbps: 3000, codec: null);

        Assert.Contains("resolution=pending", text);
        Assert.Contains("codec=pending", text);
        Assert.Contains("configured_fps=60", text);
        Assert.Contains("configured_bitrate_kbps=3000", text);
    }

    [Fact]
    public void FormatConnectionBlock_ReportsDirect_WhenNotRelayed()
    {
        var text = RemoteSessionTelemetry.FormatConnectionBlock(isRelayed: false, "host", "srflx");

        Assert.Contains("type=DIRECT", text);
        Assert.Contains("local_candidate_type=host", text);
        Assert.Contains("remote_candidate_type=srflx", text);
    }

    [Fact]
    public void FormatConnectionBlock_ReportsTurn_WhenRelayed()
    {
        var text = RemoteSessionTelemetry.FormatConnectionBlock(isRelayed: true, "relay", "relay");

        Assert.Contains("type=TURN", text);
        Assert.Contains("local_candidate_type=relay", text);
        Assert.Contains("remote_candidate_type=relay", text);
    }

    [Fact]
    public void FormatConnectionBlock_OmitsCandidateLines_WhenNotYetKnown()
    {
        var text = RemoteSessionTelemetry.FormatConnectionBlock(isRelayed: false, null, null);

        Assert.DoesNotContain("local_candidate_type", text);
        Assert.DoesNotContain("remote_candidate_type", text);
    }

    [Fact]
    public void FormatConnectionChanged_ShowsPreviousAndCurrent()
    {
        var text = RemoteSessionTelemetry.FormatConnectionChanged(previousIsRelayed: false, currentIsRelayed: true);

        Assert.StartsWith("CONNECTION_CHANGED", text);
        Assert.Contains("previous=DIRECT", text);
        Assert.Contains("current=TURN", text);
    }

    [Fact]
    public void FormatVideoSettingsAdjusted_ShowsTheRequestedFpsAndBitrate()
    {
        var text = RemoteSessionTelemetry.FormatVideoSettingsAdjusted(fps: 15, bitrateKbps: 2000);

        Assert.StartsWith("VIDEO_SETTINGS_ADJUSTED", text);
        Assert.Contains("fps=15", text);
        Assert.Contains("bitrate_kbps=2000", text);
    }

    [Fact]
    public void FormatMetrics_LabelsLatencyAsUnavailable_WhenNoRttComputed()
    {
        var text = RemoteSessionTelemetry.FormatMetrics(
            DateTimeOffset.Now, WebRtcRole.Answerer, rtt: null, fps: 59.8, bitrateKbps: 2987,
            jitterUnits: null, packetsLost: null,
            framesDecoded: 60, framesRendered: 58, framesDropped: 2, bytesReceived: 373375);

        Assert.Contains("latency_source=unavailable", text);
        Assert.Contains("latency_ms=", text);
        Assert.DoesNotContain("latency_source=rtcp_sr_dlsr", text);
        Assert.Contains("user=REQUESTER", text);
    }

    [Fact]
    public void FormatMetrics_LabelsLatencyAsRtcpDerived_WhenRttIsComputed()
    {
        var text = RemoteSessionTelemetry.FormatMetrics(
            DateTimeOffset.Now, WebRtcRole.Offerer, rtt: TimeSpan.FromMilliseconds(24), fps: 59.8, bitrateKbps: 2987,
            jitterUnits: 189, packetsLost: 0,
            framesDecoded: 60, framesRendered: 60, framesDropped: 0, bytesReceived: 373375);

        Assert.Contains("latency_ms=24", text);
        Assert.Contains("latency_source=rtcp_sr_dlsr", text);
        Assert.Contains("user=TARGET", text);
    }

    [Fact]
    public void FormatMetrics_ComputesFramesDropped_AsDecodedMinusRendered()
    {
        var text = RemoteSessionTelemetry.FormatMetrics(
            DateTimeOffset.Now, WebRtcRole.Answerer, rtt: null, fps: 60, bitrateKbps: 3000,
            jitterUnits: null, packetsLost: null,
            framesDecoded: 60, framesRendered: 55, framesDropped: 5, bytesReceived: 1000);

        Assert.Contains("frames_decoded=60", text);
        Assert.Contains("frames_rendered=55", text);
        Assert.Contains("frames_dropped=5", text);
    }

    [Fact]
    public void FormatSessionEnded_ReportsTerminationReasonAndTotals()
    {
        var text = RemoteSessionTelemetry.FormatSessionEnded(
            TimeSpan.FromSeconds(421), "normal", finalIsRelayed: false,
            totalFramesDecoded: 25260, totalFramesRendered: 25100, totalBytesReceived: 150_000_000, totalPacketsLost: 3);

        Assert.StartsWith("SESSION_ENDED", text);
        Assert.Contains("duration_sec=421", text);
        Assert.Contains("termination_reason=normal", text);
        Assert.Contains("final_connection_type=DIRECT", text);
        Assert.Contains("total_frames_decoded=25260", text);
        Assert.Contains("total_frames_rendered=25100", text);
        Assert.Contains("total_frames_dropped=160", text);
        Assert.Contains("total_packets_lost=3", text);
    }

    [Fact]
    public void FormatPipelineMetrics_ContainsEncodedAndSentFps()
    {
        var text = RemoteSessionTelemetry.FormatPipelineMetrics(DateTimeOffset.Now, encodedFps: 17.2, sentFps: 17.0);

        Assert.StartsWith("PIPELINE_METRICS", text);
        Assert.Contains("encoded_fps=17.2", text);
        Assert.Contains("sent_fps=17.0", text);
    }

    [Fact]
    public void FormatSessionEnded_ReportsUnavailable_WhenNoPacketsLostEverObserved()
    {
        var text = RemoteSessionTelemetry.FormatSessionEnded(
            TimeSpan.FromSeconds(10), "normal", finalIsRelayed: false,
            totalFramesDecoded: 100, totalFramesRendered: 100, totalBytesReceived: 1000, totalPacketsLost: null);

        Assert.Contains("total_packets_lost=unavailable", text);
    }
}
