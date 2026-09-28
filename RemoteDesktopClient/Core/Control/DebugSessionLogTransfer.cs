using System.Diagnostics;

namespace RemoteDesktopClient.Core.Control;

public sealed class DebugSessionLogTransfer
{
    private readonly bool _isTarget;
    private readonly Action<ControlMessage> _send;
    private readonly Func<ulong> _bufferedAmount;

    private readonly object _lock = new();
    private string? _incomingTransferId;
    private MemoryStream? _incomingBuffer;
    private int _incomingNextChunkIndex;
    private long _incomingDeclaredSize;
    private TaskCompletionSource<byte[]?> _received = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public DebugSessionLogTransfer(bool isTarget, Action<ControlMessage> send, Func<ulong> bufferedAmount)
    {
        _isTarget = isTarget;
        _send = send;
        _bufferedAmount = bufferedAmount;
    }

    public async Task TrySendLogAsync(string logFilePath, TimeSpan timeout)
    {
        if (!_isTarget)
            return;
        try
        {
            using var cts = new CancellationTokenSource(timeout);
            await SendLogAsync(logFilePath, cts.Token).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"DebugSessionLogTransfer: failed to send debug log - continuing session teardown. {ex}");
        }
    }

    private async Task SendLogAsync(string logFilePath, CancellationToken cancellationToken)
    {
        if (!File.Exists(logFilePath))
            return;

        var bytes = await File.ReadAllBytesAsync(logFilePath, cancellationToken).ConfigureAwait(false);
        var transferId = Guid.NewGuid().ToString("N");
        _send(new DebugLogStartMessage(transferId, bytes.LongLength));

        var chunkIndex = 0;
        for (var offset = 0; offset < bytes.Length; offset += FileTransferPolicy.ChunkSizeBytes)
        {
            while (_bufferedAmount() > FileTransferPolicy.MaxBufferedBytes)
                await Task.Delay(5, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();

            var length = Math.Min(FileTransferPolicy.ChunkSizeBytes, bytes.Length - offset);
            _send(new DebugLogChunkMessage(transferId, chunkIndex++, bytes.AsSpan(offset, length).ToArray()));
        }

        _send(new DebugLogCompleteMessage(transferId, chunkIndex));

        while (_bufferedAmount() > 0)
            await Task.Delay(5, cancellationToken).ConfigureAwait(false);
    }

    public bool Handle(ControlMessage message)
    {
        if (_isTarget)
            return false;

        switch (message)
        {
            case DebugLogStartMessage start:
                lock (_lock)
                {
                    _incomingTransferId = start.TransferId;
                    _incomingBuffer = new MemoryStream();
                    _incomingNextChunkIndex = 0;
                    _incomingDeclaredSize = start.FileSize;
                }
                Trace.WriteLine($"DebugSessionLogTransfer: receiving debug log transfer_id={start.TransferId} declared_size={start.FileSize}");
                return true;

            case DebugLogChunkMessage chunk:
                lock (_lock)
                {
                    if (_incomingBuffer is null || chunk.TransferId != _incomingTransferId || chunk.ChunkIndex != _incomingNextChunkIndex)
                    {
                        Trace.WriteLine($"DebugSessionLogTransfer: discarding out-of-order/stale chunk transfer_id={chunk.TransferId} chunk_index={chunk.ChunkIndex} (expected transfer_id={_incomingTransferId}, expected chunk_index={_incomingNextChunkIndex})");
                        return true;
                    }
                    _incomingBuffer.Write(chunk.Data, 0, chunk.Data.Length);
                    _incomingNextChunkIndex++;
                }
                return true;

            case DebugLogCompleteMessage complete:
                byte[]? result = null;
                long bufferedLength = -1;
                int nextChunkIndex = -1;
                lock (_lock)
                {
                    if (_incomingBuffer is { } buffer)
                    {
                        bufferedLength = buffer.Length;
                        nextChunkIndex = _incomingNextChunkIndex;
                        if (complete.TransferId == _incomingTransferId && complete.TotalChunks == _incomingNextChunkIndex && buffer.Length == _incomingDeclaredSize)
                            result = buffer.ToArray();
                    }
                    _incomingBuffer = null;
                    _incomingTransferId = null;
                }
                if (result is not null)
                {
                    Trace.WriteLine($"DebugSessionLogTransfer: received debug log transfer_id={complete.TransferId} size={result.Length}");
                    _received.TrySetResult(result);
                }
                else
                {
                    Trace.WriteLine($"DebugSessionLogTransfer: rejecting incomplete/mismatched debug log transfer_id={complete.TransferId} declared_total_chunks={complete.TotalChunks} received_chunks={nextChunkIndex} declared_size={_incomingDeclaredSize} received_bytes={bufferedLength}");
                }
                return true;

            default:
                return false;
        }
    }

    public async Task<byte[]?> WaitForLogAsync(TimeSpan timeout)
    {
        if (_isTarget)
            return null;
        try
        {
            var completed = await Task.WhenAny(_received.Task, Task.Delay(timeout)).ConfigureAwait(false);
            return completed == _received.Task ? await _received.Task.ConfigureAwait(false) : null;
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"DebugSessionLogTransfer: failed while waiting for debug log. {ex}");
            return null;
        }
    }
}
