using System.Diagnostics;
using RemoteDesktopClient.Core.Control;

namespace RemoteDesktopClient.Tests.Core;

public sealed class DebugSessionLogTransferTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "RemoteConnectTests", Guid.NewGuid().ToString("N"));

    public DebugSessionLogTransferTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch (IOException) { }
    }

    [Fact]
    public async Task FullTransfer_RoundTrips_ByteForByte()
    {
        var (target, requester) = CreatePair();
        var logPath = CreateFile("session-log.txt", 500);

        await target.Transfer.TrySendLogAsync(logPath, TimeSpan.FromSeconds(5));
        var received = await requester.Transfer.WaitForLogAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(File.ReadAllBytes(logPath), received);
        Assert.IsType<DebugLogStartMessage>(target.Sent[0]);
        Assert.IsType<DebugLogCompleteMessage>(target.Sent[^1]);
    }

    [Fact]
    public async Task Transfer_IsChunked_ForLargeLogs()
    {
        var (target, requester) = CreatePair();
        var logPath = CreateFile("big-log.txt", FileTransferPolicy.ChunkSizeBytes * 3 + 42);

        await target.Transfer.TrySendLogAsync(logPath, TimeSpan.FromSeconds(5));
        var received = await requester.Transfer.WaitForLogAsync(TimeSpan.FromSeconds(5));

        var chunks = target.Sent.OfType<DebugLogChunkMessage>().ToList();
        Assert.Equal(4, chunks.Count);
        Assert.All(chunks, c => Assert.True(c.Data.Length <= FileTransferPolicy.ChunkSizeBytes));
        Assert.Equal(Enumerable.Range(0, 4), chunks.Select(c => c.ChunkIndex));
        Assert.Equal(File.ReadAllBytes(logPath), received);
    }

    [Fact]
    public async Task EmptyLogFile_IsTransferred_AsEmptyBytes()
    {
        var (target, requester) = CreatePair();
        var logPath = CreateFile("empty.txt", 0);

        await target.Transfer.TrySendLogAsync(logPath, TimeSpan.FromSeconds(5));
        var received = await requester.Transfer.WaitForLogAsync(TimeSpan.FromSeconds(5));

        Assert.Empty(target.Sent.OfType<DebugLogChunkMessage>());
        Assert.Empty(received!);
    }

    [Fact]
    public async Task MissingSourceFile_IsACleanNoOp_NothingSent()
    {
        var (target, requester) = CreatePair();

        await target.Transfer.TrySendLogAsync(Path.Combine(_root, "does-not-exist.txt"), TimeSpan.FromSeconds(1));

        Assert.Empty(target.Sent);
        var received = await requester.Transfer.WaitForLogAsync(TimeSpan.FromMilliseconds(200));
        Assert.Null(received);
    }

    [Fact]
    public async Task WaitForLogAsync_TimesOutCleanly_WhenNothingArrives()
    {
        var (_, requester) = CreatePair();

        var stopwatch = Stopwatch.StartNew();
        var received = await requester.Transfer.WaitForLogAsync(TimeSpan.FromMilliseconds(200));
        stopwatch.Stop();

        Assert.Null(received);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task TargetInstance_TrySendLogAsync_IsSelfGated_OnRequesterRoleInstance()
    {
        var (_, requester) = CreatePair();
        var logPath = CreateFile("log.txt", 100);

        await requester.Transfer.TrySendLogAsync(logPath, TimeSpan.FromSeconds(1));

        Assert.Empty(requester.Sent);
    }

    [Fact]
    public async Task WaitForLogAsync_ReturnsImmediately_OnTargetRoleInstance()
    {
        var (target, _) = CreatePair();

        var stopwatch = Stopwatch.StartNew();
        var received = await target.Transfer.WaitForLogAsync(TimeSpan.FromSeconds(10));
        stopwatch.Stop();

        Assert.Null(received);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(1), "a target-role instance must not wait at all - it never receives.");
    }

    [Fact]
    public void TargetInstance_Handle_IsSelfGated_ReturnsFalse()
    {
        var (target, _) = CreatePair();

        Assert.False(target.Transfer.Handle(new DebugLogStartMessage("t1", 10)));
    }

    private (Peer Target, Peer Requester) CreatePair()
    {
        var target = new Peer();
        var requester = new Peer();
        target.Transfer = target.Create(isTarget: true, requester);
        requester.Transfer = requester.Create(isTarget: false, target);
        return (target, requester);
    }

    private string CreateFile(string name, int size)
    {
        var path = Path.Combine(_root, name);
        var bytes = new byte[size];
        new Random(size ^ name.GetHashCode()).NextBytes(bytes);
        File.WriteAllBytes(path, bytes);
        return path;
    }

    private sealed class Peer
    {
        public DebugSessionLogTransfer Transfer { get; set; } = null!;
        public List<ControlMessage> Sent { get; } = [];

        public DebugSessionLogTransfer Create(bool isTarget, Peer other) => new(
            isTarget,
            send: message =>
            {
                var json = ControlMessageSerializer.Serialize(message);
                var decoded = ControlMessageSerializer.TryDeserialize(json) ?? throw new InvalidOperationException($"not a control message: {json}");
                Sent.Add(decoded);
                other.Transfer.Handle(decoded);
            },
            bufferedAmount: () => 0);
    }
}
