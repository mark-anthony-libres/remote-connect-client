namespace RemoteDesktopClient.WebRTC;

public static class RtcpRoundTripCalculator
{
    private static readonly DateTime NtpEpoch = new(1900, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    public static TimeSpan? ComputeRoundTripTime(uint lastSenderReportTimestamp, uint delaySinceLastSenderReport, DateTime nowUtc)
    {
        if (lastSenderReportTimestamp == 0)
            return null;

        var now32 = ToNtpMiddle32Bits(nowUtc);
        long rttUnits = (long)now32 - lastSenderReportTimestamp - delaySinceLastSenderReport;
        var rttSeconds = rttUnits / 65536.0;

        return TimeSpan.FromSeconds(Math.Max(0, rttSeconds));
    }

    private static uint ToNtpMiddle32Bits(DateTime utc)
    {
        var secondsSinceNtpEpoch = (utc - NtpEpoch).TotalSeconds;
        var wholeSeconds = (uint)secondsSinceNtpEpoch;
        var fraction = secondsSinceNtpEpoch - wholeSeconds;
        var fractionBits = (uint)(fraction * 4294967296.0);
        return ((wholeSeconds & 0xFFFF) << 16) | (fractionBits >> 16);
    }
}
