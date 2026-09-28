using System.Diagnostics;

namespace RemoteDesktopClient.Core.Control;

public enum FileTransferSendResult
{
    NothingToSend,
    Completed,
    RejectedSizeLimit,
    RejectedByPeer,
    Cancelled,
    Failed,
}

public sealed class ClipboardFileTransfer : IDisposable
{
    private readonly bool _isTarget;
    private readonly int _maxTurnMb;
    private readonly Func<bool> _isRelayed;
    private readonly Action<ControlMessage> _send;
    private readonly Func<ulong> _bufferedAmount;
    private readonly string _stagingRoot;
    private readonly Action<string> _log;

    private readonly object _lock = new();

    private CancellationTokenSource? _outgoingCts;
    private string? _outgoingBatchId;
    private string? _outgoingTransferId;
    private bool _outgoingRejectedByPeer;

    private IncomingBatch? _incomingBatch;
    private string? _rejectedIncomingBatchId;

    private bool _disposed;

    public event Action<IReadOnlyList<string>>? FilesReceived;

    public event Action<FileTransferAlert>? AlertRaised;

    public ClipboardFileTransfer(
        bool isTarget,
        int maxTurnMb,
        Func<bool> isRelayed,
        Action<ControlMessage> send,
        Func<ulong> bufferedAmount,
        string stagingRoot,
        Action<string>? log = null)
    {
        if (maxTurnMb <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxTurnMb), maxTurnMb, "maxTurnMb must be positive.");

        _isTarget = isTarget;
        _maxTurnMb = maxTurnMb;
        _isRelayed = isRelayed;
        _send = send;
        _bufferedAmount = bufferedAmount;
        _stagingRoot = stagingRoot;
        _log = log ?? (line => Trace.WriteLine(line));
    }

    public static string DefaultStagingRoot => Path.Combine(Path.GetTempPath(), "RemoteConnect", "FileTransfers");

    private FileTransferDirection OutgoingDirection => _isTarget ? FileTransferDirection.TargetToRequester : FileTransferDirection.RequesterToTarget;

    private FileTransferDirection IncomingDirection => _isTarget ? FileTransferDirection.RequesterToTarget : FileTransferDirection.TargetToRequester;

    private bool IsRequester => !_isTarget;

    public async Task<FileTransferSendResult> SendFilesAsync(IReadOnlyList<string> paths)
    {
        var files = new List<FileInfo>();
        foreach (var path in paths)
        {
            if (File.Exists(path))
                files.Add(new FileInfo(path));
            else
                _log($"[FileTransfer] skipped reason={(Directory.Exists(path) ? "folder_not_supported" : "not_found")} file_name={Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar))}");
        }
        if (files.Count == 0)
            return FileTransferSendResult.NothingToSend;
        if (files.Count > FileTransferPolicy.MaxFilesPerBatch)
        {
            _log($"[FileTransfer] skipped reason=too_many_files file_count={files.Count} max={FileTransferPolicy.MaxFilesPerBatch}");
            return FileTransferSendResult.NothingToSend;
        }

        var direction = OutgoingDirection;
        var isRelayed = _isRelayed();
        var batchId = NewId();
        LogTransferHeader(direction, isRelayed);
        foreach (var file in files)
            LogFile(file.Name, file.Length);

        if (files.FirstOrDefault(f => !FileTransferPolicy.IsAllowed(f.Length, isRelayed, _maxTurnMb)) is { } oversized)
        {
            _log($"[FileTransfer] rejected reason=size_limit file_name={oversized.Name} file_size={oversized.Length} limit_mb={_maxTurnMb}");
            if (IsRequester)
                AlertRaised?.Invoke(FileTransferPolicy.SizeLimitAlert(direction, _maxTurnMb));
            else
                TrySend(new FileTransferErrorMessage(batchId, "size_limit", FileTransferPolicy.ToWire(direction), _maxTurnMb));
            return FileTransferSendResult.RejectedSizeLimit;
        }

        var cts = new CancellationTokenSource();
        lock (_lock)
        {
            if (_disposed)
            {
                cts.Dispose();
                return FileTransferSendResult.Cancelled;
            }
            _outgoingCts?.Cancel();
            _outgoingCts = cts;
            _outgoingBatchId = batchId;
            _outgoingTransferId = null;
            _outgoingRejectedByPeer = false;
        }

        string? currentTransferId = null;
        try
        {
            for (var index = 0; index < files.Count; index++)
            {
                var file = files[index];
                currentTransferId = NewId();
                lock (_lock)
                    _outgoingTransferId = currentTransferId;

                await SendOneFileAsync(file, currentTransferId, batchId, index, files.Count, cts.Token).ConfigureAwait(false);
            }
            return FileTransferSendResult.Completed;
        }
        catch (OperationCanceledException)
        {
            bool rejectedByPeer;
            lock (_lock)
                rejectedByPeer = _outgoingRejectedByPeer;
            if (currentTransferId is not null)
                TrySend(new FileTransferCancelMessage(currentTransferId, rejectedByPeer ? "rejected" : "cancelled"));
            _log($"[FileTransfer] cancelled transfer_id={currentTransferId} reason={(rejectedByPeer ? "rejected_by_peer" : "superseded")}");
            return rejectedByPeer ? FileTransferSendResult.RejectedByPeer : FileTransferSendResult.Cancelled;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or FileChangedException)
        {
            var reason = ex is FileChangedException ? "file_changed" : ex is InvalidOperationException ? "channel_closed" : "read_failed";
            _log($"[FileTransfer] failed transfer_id={currentTransferId} reason={reason}");
            if (currentTransferId is not null)
                TrySend(new FileTransferCancelMessage(currentTransferId, reason));
            return FileTransferSendResult.Failed;
        }
        finally
        {
            lock (_lock)
            {
                if (ReferenceEquals(_outgoingCts, cts))
                {
                    _outgoingCts = null;
                    _outgoingBatchId = null;
                    _outgoingTransferId = null;
                }
            }
            cts.Dispose();
        }
    }

    private async Task SendOneFileAsync(FileInfo file, string transferId, string batchId, int fileIndex, int fileCount, CancellationToken cancellationToken)
    {
        var declaredSize = file.Length;
        _send(new FileTransferStartMessage(transferId, batchId, fileIndex, fileCount, file.Name, declaredSize));
        _log($"[FileTransfer] started transfer_id={transferId} file_name={file.Name}");

        var stream = new FileStream(file.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, FileTransferPolicy.ChunkSizeBytes, useAsync: true);
        await using var _ = stream.ConfigureAwait(false);
        var buffer = new byte[FileTransferPolicy.ChunkSizeBytes];
        long sentBytes = 0;
        var chunkIndex = 0;
        while (true)
        {
            var read = await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            if (read == 0)
                break;

            sentBytes += read;
            if (sentBytes > declaredSize)
                throw new FileChangedException();

            while (_bufferedAmount() > FileTransferPolicy.MaxBufferedBytes)
                await Task.Delay(5, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();

            _send(new FileTransferChunkMessage(transferId, chunkIndex++, buffer.AsSpan(0, read).ToArray()));
        }

        if (sentBytes != declaredSize)
            throw new FileChangedException();

        cancellationToken.ThrowIfCancellationRequested();
        _send(new FileTransferCompleteMessage(transferId, chunkIndex));
        _log($"[FileTransfer] completed transfer_id={transferId} file_name={file.Name} file_size={declaredSize}");
    }

    public bool Handle(ControlMessage message)
    {
        switch (message)
        {
            case FileTransferStartMessage start:
                OnStart(start);
                return true;
            case FileTransferChunkMessage chunk:
                OnChunk(chunk);
                return true;
            case FileTransferCompleteMessage complete:
                OnComplete(complete);
                return true;
            case FileTransferCancelMessage cancel:
                OnCancel(cancel);
                return true;
            case FileTransferErrorMessage error:
                OnError(error);
                return true;
            default:
                return false;
        }
    }

    private void OnStart(FileTransferStartMessage start)
    {
        var direction = IncomingDirection;
        var isRelayed = _isRelayed();
        LogTransferHeader(direction, isRelayed);
        LogFile(start.FileName, start.FileSize);

        lock (_lock)
        {
            if (_disposed || start.BatchId == _rejectedIncomingBatchId)
                return;

            if (_incomingBatch is { } previous && previous.BatchId != start.BatchId)
                AbortIncomingLocked("superseded");
        }

        var invalidReason = start.FileSize < 0 ? "invalid_size"
            : !FileTransferPolicy.IsSafeFileName(start.FileName) ? "invalid_file_name"
            : start.FileCount < 1 || start.FileCount > FileTransferPolicy.MaxFilesPerBatch
                || start.FileIndex < 0 || start.FileIndex >= start.FileCount ? "invalid_index"
            : null;
        if (invalidReason is not null)
        {
            RejectIncoming(start.TransferId, start.BatchId, invalidReason, direction);
            return;
        }

        if (!FileTransferPolicy.IsAllowed(start.FileSize, isRelayed, _maxTurnMb))
        {
            _log($"[FileTransfer] rejected reason=size_limit file_name={start.FileName} file_size={start.FileSize} limit_mb={_maxTurnMb}");
            RejectIncoming(start.TransferId, start.BatchId, "size_limit", direction);
            if (IsRequester)
                AlertRaised?.Invoke(FileTransferPolicy.SizeLimitAlert(direction, _maxTurnMb));
            return;
        }

        string? failure = null;
        lock (_lock)
        {
            if (_disposed)
                return;

            var batch = _incomingBatch ??= new IncomingBatch(start.BatchId, start.FileCount, Path.Combine(_stagingRoot, start.BatchId));
            if (batch.FileCount != start.FileCount || batch.Current is not null || batch.CompletedPaths[start.FileIndex] is not null)
            {
                failure = "out_of_order";
            }
            else
            {
                try
                {
                    var fileDirectory = Path.Combine(batch.Directory, start.FileIndex.ToString(System.Globalization.CultureInfo.InvariantCulture));
                    Directory.CreateDirectory(fileDirectory);
                    var path = Path.Combine(fileDirectory, start.FileName);
                    var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                    batch.Current = new IncomingFile(start.TransferId, start.FileIndex, start.FileName, start.FileSize, path, stream);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    failure = "write_failed";
                }
            }
        }

        if (failure is not null)
        {
            RejectIncoming(start.TransferId, start.BatchId, failure, direction);
            return;
        }
        _log($"[FileTransfer] started transfer_id={start.TransferId} file_name={start.FileName}");
    }

    private void OnChunk(FileTransferChunkMessage chunk)
    {
        string? failure = null;
        string? batchId = null;
        lock (_lock)
        {
            if (_incomingBatch?.Current is not { } file || file.TransferId != chunk.TransferId)
                return;

            batchId = _incomingBatch.BatchId;
            if (chunk.ChunkIndex != file.NextChunkIndex)
            {
                failure = "out_of_order";
            }
            else if (file.WrittenBytes + chunk.Data.Length > file.DeclaredSize)
            {
                failure = "size_mismatch";
            }
            else
            {
                try
                {
                    file.Stream.Write(chunk.Data, 0, chunk.Data.Length);
                    file.WrittenBytes += chunk.Data.Length;
                    file.NextChunkIndex++;
                }
                catch (IOException)
                {
                    failure = "write_failed";
                }
            }
        }

        if (failure is not null)
            RejectIncoming(chunk.TransferId, batchId!, failure, IncomingDirection);
    }

    private void OnComplete(FileTransferCompleteMessage complete)
    {
        string? failure = null;
        string? batchId = null;
        IReadOnlyList<string>? finishedBatch = null;
        string? finishedDirectory = null;
        lock (_lock)
        {
            if (_incomingBatch is not { Current: { } file } batch || file.TransferId != complete.TransferId)
                return;

            batchId = batch.BatchId;
            if (file.WrittenBytes != file.DeclaredSize || complete.TotalChunks != file.NextChunkIndex)
            {
                failure = "size_mismatch";
            }
            else
            {
                try
                {
                    file.Stream.Dispose();
                }
                catch (IOException)
                {
                    failure = "write_failed";
                }

                if (failure is null)
                {
                    batch.CompletedPaths[file.FileIndex] = file.Path;
                    batch.Current = null;
                    _log($"[FileTransfer] completed transfer_id={file.TransferId} file_name={file.FileName} file_size={file.DeclaredSize}");

                    if (batch.CompletedPaths.All(p => p is not null))
                    {
                        finishedBatch = batch.CompletedPaths.Select(p => p!).ToList();
                        finishedDirectory = batch.Directory;
                        _incomingBatch = null;
                    }
                }
            }
        }

        if (failure is not null)
        {
            RejectIncoming(complete.TransferId, batchId!, failure, IncomingDirection);
            return;
        }

        if (finishedBatch is not null)
        {
            PruneStagingExcept(finishedDirectory!);
            FilesReceived?.Invoke(finishedBatch);
        }
    }

    private void OnCancel(FileTransferCancelMessage cancel)
    {
        lock (_lock)
        {
            if (_incomingBatch?.Current?.TransferId != cancel.TransferId)
                return;
            AbortIncomingLocked(cancel.Reason);
        }
        _log($"[FileTransfer] cancelled transfer_id={cancel.TransferId} reason={cancel.Reason}");
    }

    private void OnError(FileTransferErrorMessage error)
    {
        _log($"[FileTransfer] rejected reason={error.Reason} by=peer transfer_id={error.TransferId}");
        lock (_lock)
        {
            if (_outgoingCts is not null && (error.TransferId == _outgoingTransferId || error.TransferId == _outgoingBatchId))
            {
                _outgoingRejectedByPeer = true;
                _outgoingCts.Cancel();
            }
        }

        if (IsRequester && error.Reason == "size_limit" && FileTransferPolicy.FromWire(error.Direction) is { } direction)
            AlertRaised?.Invoke(FileTransferPolicy.SizeLimitAlert(direction, error.LimitMb));
    }

    private void RejectIncoming(string transferId, string batchId, string reason, FileTransferDirection direction)
    {
        lock (_lock)
        {
            if (_incomingBatch?.BatchId == batchId)
                AbortIncomingLocked(reason);
            _rejectedIncomingBatchId = batchId;
        }
        if (reason != "size_limit")
            _log($"[FileTransfer] rejected reason={reason} transfer_id={transferId}");
        TrySend(new FileTransferErrorMessage(transferId, reason, FileTransferPolicy.ToWire(direction), _maxTurnMb));
    }

    private void AbortIncomingLocked(string reason)
    {
        if (_incomingBatch is not { } batch)
            return;
        _incomingBatch = null;
        batch.Current?.Stream.Dispose();
        TryDeleteDirectory(batch.Directory);
        _log($"[FileTransfer] discarded incoming batch reason={reason}");
    }

    private void PruneStagingExcept(string keepDirectory)
    {
        try
        {
            foreach (var directory in Directory.EnumerateDirectories(_stagingRoot))
            {
                if (!string.Equals(Path.GetFullPath(directory), Path.GetFullPath(keepDirectory), StringComparison.OrdinalIgnoreCase))
                    TryDeleteDirectory(directory);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }

    private void LogTransferHeader(FileTransferDirection direction, bool isRelayed)
    {
        _log($"[FileTransfer] direction={FileTransferPolicy.ToWire(direction)}");
        _log($"[FileTransfer] connection={(isRelayed ? "RELAY" : "DIRECT")}");
    }

    private void LogFile(string fileName, long fileSize)
    {
        _log($"[FileTransfer] file_name={fileName}");
        _log($"[FileTransfer] file_size={fileSize}");
    }

    private void TrySend(ControlMessage message)
    {
        try
        {
            _send(message);
        }
        catch (InvalidOperationException)
        {
        }
    }

    private static void TryDeleteDirectory(string directory)
    {
        try
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }

    private static string NewId() => Guid.NewGuid().ToString("N");

    public void Dispose()
    {
        lock (_lock)
        {
            if (_disposed)
                return;
            _disposed = true;
            _outgoingCts?.Cancel();
            AbortIncomingLocked("session_closed");
        }
    }

    private sealed class IncomingBatch(string batchId, int fileCount, string directory)
    {
        public string BatchId { get; } = batchId;
        public int FileCount { get; } = fileCount;
        public string Directory { get; } = directory;
        public string?[] CompletedPaths { get; } = new string?[fileCount];
        public IncomingFile? Current { get; set; }
    }

    private sealed class IncomingFile(string transferId, int fileIndex, string fileName, long declaredSize, string path, FileStream stream)
    {
        public string TransferId { get; } = transferId;
        public int FileIndex { get; } = fileIndex;
        public string FileName { get; } = fileName;
        public long DeclaredSize { get; } = declaredSize;
        public string Path { get; } = path;
        public FileStream Stream { get; } = stream;
        public long WrittenBytes { get; set; }
        public int NextChunkIndex { get; set; }
    }

    private sealed class FileChangedException : Exception;
}
