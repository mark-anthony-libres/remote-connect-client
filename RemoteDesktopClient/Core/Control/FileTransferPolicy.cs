namespace RemoteDesktopClient.Core.Control;

public enum FileTransferDirection
{
    TargetToRequester,
    RequesterToTarget,
}

public sealed record FileTransferAlert(string Title, string Message);

public static class FileTransferPolicy
{
    public const int ChunkSizeBytes = 16 * 1024;

    public const ulong MaxBufferedBytes = 64 * 1024;

    public const long BytesPerMb = 1024L * 1024L;

    public const int MaxFilesPerBatch = 1000;

    public static long MaxRelayedFileBytes(int maxTurnMb) => maxTurnMb * BytesPerMb;

    public static bool IsAllowed(long fileSize, bool isRelayed, int maxTurnMb) =>
        !isRelayed || fileSize <= MaxRelayedFileBytes(maxTurnMb);

    public static FileTransferAlert SizeLimitAlert(FileTransferDirection direction, int limitMb) => new(
        direction == FileTransferDirection.TargetToRequester ? "Unable to copy" : "Unable to paste",
        $"The file exceeds the {limitMb} MB limit for relay connections.");

    public static string ToWire(FileTransferDirection direction) => direction == FileTransferDirection.TargetToRequester
        ? "target-to-requester"
        : "requester-to-target";

    public static FileTransferDirection? FromWire(string? value) => value switch
    {
        "target-to-requester" => FileTransferDirection.TargetToRequester,
        "requester-to-target" => FileTransferDirection.RequesterToTarget,
        _ => null,
    };

    public static bool IsSafeFileName(string? name) =>
        !string.IsNullOrWhiteSpace(name)
        && name.Length <= 255
        && name is not ("." or "..")
        && Path.GetFileName(name) == name
        && name.IndexOfAny(Path.GetInvalidFileNameChars()) < 0;
}
