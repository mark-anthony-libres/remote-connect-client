namespace RemoteDesktopClient.Core.Control;

public static class ClipboardImageChunker
{
    private const int ChunkSizeBytes = 2048;

    public static async Task SendAsync(byte[] pngBytes, int maxBitrateKbps, Action<ControlMessage> send, CancellationToken cancellationToken = default)
    {
        var transferId = Guid.NewGuid().ToString("N");
        var totalChunks = Math.Max(1, (int)Math.Ceiling(pngBytes.Length / (double)ChunkSizeBytes));
        var bitsPerSecond = Math.Max(1, maxBitrateKbps) * 1000.0;
        var delayPerChunk = TimeSpan.FromSeconds(ChunkSizeBytes * 8.0 / bitsPerSecond);

        for (var index = 0; index < totalChunks; index++)
        {
            var offset = index * ChunkSizeBytes;
            var length = Math.Min(ChunkSizeBytes, pngBytes.Length - offset);
            var data = new byte[length];
            Buffer.BlockCopy(pngBytes, offset, data, 0, length);

            send(new ClipboardImageChunkMessage(transferId, index, totalChunks, data));

            if (index < totalChunks - 1)
                await Task.Delay(delayPerChunk, cancellationToken);
        }
    }
}
