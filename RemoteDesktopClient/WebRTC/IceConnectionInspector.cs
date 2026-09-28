using SIPSorcery.Net;

namespace RemoteDesktopClient.WebRTC;

public static class IceConnectionInspector
{
    public static bool IsUsingRelay(RTCPeerConnection pc)
    {
        var nominatedEntry = pc.GetRtpChannel()?.NominatedEntry;
        if (nominatedEntry is null)
            return false;

        return nominatedEntry.LocalCandidate?.type == RTCIceCandidateType.relay
            || nominatedEntry.RemoteCandidate?.type == RTCIceCandidateType.relay;
    }
}
