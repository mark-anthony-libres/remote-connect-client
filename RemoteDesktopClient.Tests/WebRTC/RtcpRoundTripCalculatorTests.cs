using RemoteDesktopClient.WebRTC;

namespace RemoteDesktopClient.Tests.WebRTC;

public class RtcpRoundTripCalculatorTests
{
    [Fact]
    public void ReturnsNull_WhenLastSenderReportTimestampIsZero()
    {
        var result = RtcpRoundTripCalculator.ComputeRoundTripTime(
            lastSenderReportTimestamp: 0,
            delaySinceLastSenderReport: 12345,
            nowUtc: DateTime.UtcNow);

        Assert.Null(result);
    }

    [Fact]
    public void ComputesExpectedRoundTripTime_ForAKnownExample()
    {
        var now = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

        const uint twoHundredFiftyMsInUnits = (uint)(0.25 * 65536);

        var now32 = ToNtpMiddle32BitsForTest(now);
        var lsr = now32 - twoHundredFiftyMsInUnits;

        var result = RtcpRoundTripCalculator.ComputeRoundTripTime(
            lastSenderReportTimestamp: lsr,
            delaySinceLastSenderReport: 0,
            nowUtc: now);

        Assert.NotNull(result);
        Assert.InRange(result!.Value.TotalMilliseconds, 249.9, 250.1);
    }

    [Fact]
    public void SubtractsDelaySinceLastSenderReport_FromTheComputedGap()
    {
        var now = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        const uint fourHundredMsInUnits = (uint)(0.4 * 65536);
        const uint hundredFiftyMsInUnits = (uint)(0.15 * 65536);

        var now32 = ToNtpMiddle32BitsForTest(now);
        var lsr = now32 - fourHundredMsInUnits;

        var result = RtcpRoundTripCalculator.ComputeRoundTripTime(
            lastSenderReportTimestamp: lsr,
            delaySinceLastSenderReport: hundredFiftyMsInUnits,
            nowUtc: now);

        Assert.NotNull(result);
        Assert.InRange(result!.Value.TotalMilliseconds, 249.5, 250.5);
    }

    [Fact]
    public void ClampsToZero_RatherThanReturningNegative()
    {
        var now = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var now32 = ToNtpMiddle32BitsForTest(now);

        var result = RtcpRoundTripCalculator.ComputeRoundTripTime(
            lastSenderReportTimestamp: now32,
            delaySinceLastSenderReport: (uint)(1.0 * 65536),
            nowUtc: now);

        Assert.NotNull(result);
        Assert.Equal(0, result!.Value.TotalMilliseconds);
    }

    private static uint ToNtpMiddle32BitsForTest(DateTime utc)
    {
        var ntpEpoch = new DateTime(1900, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var secondsSinceEpoch = (utc - ntpEpoch).TotalSeconds;
        var wholeSeconds = (uint)secondsSinceEpoch;
        var fraction = secondsSinceEpoch - wholeSeconds;
        var fractionBits = (uint)(fraction * 4294967296.0);
        return ((wholeSeconds & 0xFFFF) << 16) | (fractionBits >> 16);
    }
}
