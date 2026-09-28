namespace RemoteDesktopClient.Core.Control;

public sealed class ClipboardImageReassembler
{
    private string? _transferId;
    private byte[]?[] _chunks = [];
    private int _receivedCount;

    public byte[]? Add(ClipboardImageChunkMessage chunk)
    {
        if (chunk.TransferId != _transferId)
        {
            _transferId = chunk.TransferId;
            _chunks = new byte[chunk.TotalChunks][];
            _receivedCount = 0;
        }

        if (chunk.ChunkIndex < 0 || chunk.ChunkIndex >= _chunks.Length)
            return null;

        if (_chunks[chunk.ChunkIndex] is null)
        {
            _chunks[chunk.ChunkIndex] = chunk.Data;
            _receivedCount++;
        }

        if (_receivedCount < _chunks.Length)
            return null;

        var totalLength = 0;
        foreach (var part in _chunks)
            totalLength += part!.Length;

        var result = new byte[totalLength];
        var offset = 0;
        foreach (var part in _chunks)
        {
            Buffer.BlockCopy(part!, 0, result, offset, part!.Length);
            offset += part.Length;
        }

        _transferId = null;
        _chunks = [];
        _receivedCount = 0;
        return result;
    }
}
