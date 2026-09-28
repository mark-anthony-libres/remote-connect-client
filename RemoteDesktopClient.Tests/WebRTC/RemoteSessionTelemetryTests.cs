using SIPSorcery.Net;
using RemoteDesktopClient.WebRTC;

namespace RemoteDesktopClient.Tests.WebRTC;

public class RemoteSessionTelemetryTests : IDisposable
{
    private readonly List<RTCPeerConnection> _peerConnections = [];

    private RTCPeerConnection NewPeerConnection()
    {
        var pc = new RTCPeerConnection(new RTCConfiguration());
        _peerConnections.Add(pc);
        return pc;
    }

    public void Dispose()
    {
        foreach (var pc in _peerConnections)
        {
            try { pc.Close("test cleanup"); }
            catch { }
        }
    }

    private static string UniqueRequestId() => $"test-session-{Guid.NewGuid():N}";

    [Fact]
    public void StartThenStop_CreatesExactlyOneLogFile_ContainingSessionStartedAndEnded()
    {
        var requestId = UniqueRequestId();
        var telemetry = new RemoteSessionTelemetry(requestId, WebRtcRole.Answerer, "1112223333", "4445556666", "Office-PC", TimeSpan.FromSeconds(1));
        var pc = NewPeerConnection();

        telemetry.Start(pc, configuredFps: 60, configuredBitrateKbps: 3000, resolution: "1920x1080", codec: "VP8");
        telemetry.Stop("normal");

        var expectedPath = RemoteSessionTelemetry.BuildLogFilePath(requestId);
        Assert.True(File.Exists(expectedPath));
        var content = File.ReadAllText(expectedPath);
        Assert.Contains("SESSION_STARTED", content);
        Assert.Contains($"request_id={requestId}", content);
        Assert.Contains("SESSION_ENDED", content);
        Assert.Contains("termination_reason=normal", content);

        File.Delete(expectedPath);
    }

    [Fact]
    public void NeverCallingStart_ProducesNoLogFile()
    {
        var requestId = UniqueRequestId();
        var telemetry = new RemoteSessionTelemetry(requestId, WebRtcRole.Answerer, "a", "b", "c", TimeSpan.FromSeconds(1));

        telemetry.Stop("normal");

        var expectedPath = RemoteSessionTelemetry.BuildLogFilePath(requestId);
        Assert.False(File.Exists(expectedPath));
    }

    [Fact]
    public void RepeatedSessions_CreateSeparateFiles_PerRequestId()
    {
        var requestIdA = UniqueRequestId();
        var requestIdB = UniqueRequestId();
        var telemetryA = new RemoteSessionTelemetry(requestIdA, WebRtcRole.Answerer, "a", "b", "c", TimeSpan.FromSeconds(1));
        var telemetryB = new RemoteSessionTelemetry(requestIdB, WebRtcRole.Offerer, "a", "b", "c", TimeSpan.FromSeconds(1));

        telemetryA.Start(NewPeerConnection(), 60, 3000, "1920x1080", "VP8");
        telemetryA.Stop("normal");
        telemetryB.Start(NewPeerConnection(), 60, 3000, "1920x1080", "VP8");
        telemetryB.Stop("normal");

        var pathA = RemoteSessionTelemetry.BuildLogFilePath(requestIdA);
        var pathB = RemoteSessionTelemetry.BuildLogFilePath(requestIdB);
        Assert.True(File.Exists(pathA));
        Assert.True(File.Exists(pathB));
        Assert.NotEqual(pathA, pathB);

        File.Delete(pathA);
        File.Delete(pathB);
    }

    [Fact]
    public void CallingStartTwice_DoesNotWriteASecondSessionStartedBlock()
    {
        var requestId = UniqueRequestId();
        var telemetry = new RemoteSessionTelemetry(requestId, WebRtcRole.Answerer, "a", "b", "c", TimeSpan.FromSeconds(1));
        var pc = NewPeerConnection();

        telemetry.Start(pc, 60, 3000, "1920x1080", "VP8");
        telemetry.Start(pc, 60, 3000, "1920x1080", "VP8");
        telemetry.Stop("normal");

        var path = RemoteSessionTelemetry.BuildLogFilePath(requestId);
        var occurrences = File.ReadAllText(path).Split("SESSION_STARTED").Length - 1;
        Assert.Equal(1, occurrences);

        File.Delete(path);
    }

    [Fact]
    public void CallingStopTwice_DoesNotThrow_AndWritesOnlyOneSessionEndedBlock()
    {
        var requestId = UniqueRequestId();
        var telemetry = new RemoteSessionTelemetry(requestId, WebRtcRole.Answerer, "a", "b", "c", TimeSpan.FromSeconds(1));
        var pc = NewPeerConnection();
        telemetry.Start(pc, 60, 3000, "1920x1080", "VP8");

        telemetry.Stop("normal");
        var exception = Record.Exception(() => telemetry.Stop("normal"));

        Assert.Null(exception);
        var path = RemoteSessionTelemetry.BuildLogFilePath(requestId);
        var occurrences = File.ReadAllText(path).Split("SESSION_ENDED").Length - 1;
        Assert.Equal(1, occurrences);

        File.Delete(path);
    }

    [Fact]
    public void ConcurrentCounterUpdates_AreAllCaptured_WithNoLostIncrements()
    {
        var requestId = UniqueRequestId();
        var telemetry = new RemoteSessionTelemetry(requestId, WebRtcRole.Answerer, "a", "b", "c", TimeSpan.FromSeconds(30));
        var pc = NewPeerConnection();
        telemetry.Start(pc, 60, 3000, "1920x1080", "VP8");

        const int threadCount = 20;
        const int incrementsPerThread = 2000;
        Parallel.For(0, threadCount, _ =>
        {
            for (var i = 0; i < incrementsPerThread; i++)
            {
                telemetry.OnVideoFrameDecoded();
                telemetry.OnVideoBytesReceived(100);
            }
        });

        telemetry.Stop("normal");

        var path = RemoteSessionTelemetry.BuildLogFilePath(requestId);
        var content = File.ReadAllText(path);
        Assert.Contains($"total_frames_decoded={threadCount * incrementsPerThread}", content);
        Assert.Contains($"total_bytes_received={threadCount * incrementsPerThread * 100}", content);

        File.Delete(path);
    }

    [Fact]
    public async Task Sampler_ComputesActualFpsAndBitrate_NotConfiguredValues()
    {
        var requestId = UniqueRequestId();
        var telemetry = new RemoteSessionTelemetry(requestId, WebRtcRole.Answerer, "a", "b", "c", TimeSpan.FromMilliseconds(150));
        var pc = NewPeerConnection();
        telemetry.Start(pc, configuredFps: 9999, configuredBitrateKbps: 9999, resolution: "1920x1080", codec: "VP8");

        for (var i = 0; i < 10; i++)
        {
            telemetry.OnVideoFrameDecoded();
            telemetry.OnVideoFrameRendered();
            telemetry.OnVideoBytesReceived(1000);
        }
        await Task.Delay(400);

        telemetry.Stop("normal");

        var path = RemoteSessionTelemetry.BuildLogFilePath(requestId);
        var content = File.ReadAllText(path);
        Assert.Contains("METRICS", content);
        var metricsBlock = content[content.IndexOf("METRICS", StringComparison.Ordinal)..];
        Assert.DoesNotContain("fps=9999", metricsBlock);
        Assert.DoesNotContain("bitrate_kbps=9999", metricsBlock);

        File.Delete(path);
    }

    [Fact]
    public void ConnectionBlock_ReportsDirect_ForAnUnconnectedPeerConnection()
    {
        var requestId = UniqueRequestId();
        var telemetry = new RemoteSessionTelemetry(requestId, WebRtcRole.Answerer, "a", "b", "c", TimeSpan.FromSeconds(1));
        var pc = NewPeerConnection();

        telemetry.Start(pc, 60, 3000, "1920x1080", "VP8");
        telemetry.Stop("normal");

        var path = RemoteSessionTelemetry.BuildLogFilePath(requestId);
        var content = File.ReadAllText(path);
        Assert.Contains("CONNECTION", content);
        Assert.Contains("type=DIRECT", content);

        File.Delete(path);
    }

    [Fact]
    public void StoppingWithADifferentTerminationReason_IsReflectedInTheLog()
    {
        var requestId = UniqueRequestId();
        var telemetry = new RemoteSessionTelemetry(requestId, WebRtcRole.Offerer, "a", "b", "c", TimeSpan.FromSeconds(1));
        var pc = NewPeerConnection();
        telemetry.Start(pc, 60, 3000, "1920x1080", "VP8");

        telemetry.Stop("target_disconnected");

        var path = RemoteSessionTelemetry.BuildLogFilePath(requestId);
        Assert.Contains("termination_reason=target_disconnected", File.ReadAllText(path));

        File.Delete(path);
    }

    [Fact]
    public async Task PipelineMetrics_OnlyLoggedForOffererRole_WithRealEncodedAndSentCounts()
    {
        var requestId = UniqueRequestId();
        var telemetry = new RemoteSessionTelemetry(requestId, WebRtcRole.Offerer, "a", "b", "c", TimeSpan.FromMilliseconds(150));
        var pc = NewPeerConnection();
        telemetry.Start(pc, 40, 1000, "1152x864", "VP8");

        for (var i = 0; i < 10; i++)
        {
            telemetry.OnVideoFrameEncoded();
            telemetry.OnVideoFrameSent();
        }
        await Task.Delay(400);

        telemetry.Stop("normal");

        var path = RemoteSessionTelemetry.BuildLogFilePath(requestId);
        var content = File.ReadAllText(path);
        Assert.Contains("PIPELINE_METRICS", content);
        Assert.Contains("encoded_fps=", content);
        Assert.Contains("sent_fps=", content);

        File.Delete(path);
    }

    [Fact]
    public async Task PipelineMetrics_NeverLoggedForAnswererRole()
    {
        var requestId = UniqueRequestId();
        var telemetry = new RemoteSessionTelemetry(requestId, WebRtcRole.Answerer, "a", "b", "c", TimeSpan.FromMilliseconds(150));
        var pc = NewPeerConnection();
        telemetry.Start(pc, 40, 1000, "1152x864", "VP8");
        await Task.Delay(400);
        telemetry.Stop("normal");

        var path = RemoteSessionTelemetry.BuildLogFilePath(requestId);
        var content = File.ReadAllText(path);
        Assert.DoesNotContain("PIPELINE_METRICS", content);

        File.Delete(path);
    }

    [Fact]
    public void StartFailure_NeverThrows_AndSubsequentCallsAreSafeNoOps()
    {
        var requestId = UniqueRequestId();
        var path = RemoteSessionTelemetry.BuildLogFilePath(requestId);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var blockingHandle = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);

        var telemetry = new RemoteSessionTelemetry(requestId, WebRtcRole.Answerer, "a", "b", "c", TimeSpan.FromSeconds(1));
        var pc = NewPeerConnection();

        var startException = Record.Exception(() => telemetry.Start(pc, 60, 3000, "1920x1080", "VP8"));
        var pokeException = Record.Exception(() =>
        {
            telemetry.OnVideoFrameDecoded();
            telemetry.OnVideoBytesReceived(100);
            telemetry.OnVideoFrameRendered();
            telemetry.OnConnectionStateChanged(RTCPeerConnectionState.connected);
            telemetry.OnIceConnectionStateChanged(RTCIceConnectionState.connected);
            telemetry.OnDataChannelOpened();
            telemetry.OnDataChannelClosed();
        });
        var stopException = Record.Exception(() => telemetry.Stop("normal"));

        Assert.Null(startException);
        Assert.Null(pokeException);
        Assert.Null(stopException);

        blockingHandle.Dispose();
        if (File.Exists(path))
            File.Delete(path);
    }
}
