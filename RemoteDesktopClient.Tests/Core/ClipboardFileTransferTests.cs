using RemoteDesktopClient.Core.Control;

namespace RemoteDesktopClient.Tests.Core;

public sealed class ClipboardFileTransferTests : IDisposable
{
    private const int Mb = 1024 * 1024;
    private readonly string _root = Path.Combine(Path.GetTempPath(), "RemoteConnectTests", Guid.NewGuid().ToString("N"));

    public ClipboardFileTransferTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch (IOException) { }
    }

    [Fact]
    public async Task Direct_TargetToRequester_CopiesFileExactly_WithNoSizeLimit()
    {
        var (target, requester) = CreatePair(relayed: false, targetLimitMb: 1, requesterLimitMb: 1);
        var source = CreateFile("target-src", "Quarterly report (final).xlsx", 3 * Mb + 1234);

        var result = await target.Transfer.SendFilesAsync([source]);

        Assert.Equal(FileTransferSendResult.Completed, result);
        var received = Assert.Single(Assert.Single(requester.Received));
        AssertSameFile(source, received);
        Assert.Empty(requester.Alerts);
        Assert.Contains("[FileTransfer] direction=target-to-requester", target.Log);
        Assert.Contains("[FileTransfer] connection=DIRECT", target.Log);
        Assert.Contains("[FileTransfer] file_name=Quarterly report (final).xlsx", target.Log);
        Assert.Contains($"[FileTransfer] file_size={3 * Mb + 1234}", target.Log);
        Assert.Contains(target.Log, l => l.StartsWith("[FileTransfer] started"));
        Assert.Contains(target.Log, l => l.StartsWith("[FileTransfer] completed"));
    }

    [Fact]
    public async Task Direct_RequesterToTarget_PastesFileExactly_WithNoSizeLimit()
    {
        var (target, requester) = CreatePair(relayed: false, targetLimitMb: 1, requesterLimitMb: 1);
        var source = CreateFile("requester-src", "photo ☀ 2024.png", 2 * Mb + 7);

        var result = await requester.Transfer.SendFilesAsync([source]);

        Assert.Equal(FileTransferSendResult.Completed, result);
        AssertSameFile(source, Assert.Single(Assert.Single(target.Received)));
        Assert.Contains("[FileTransfer] direction=requester-to-target", requester.Log);
        Assert.Contains("[FileTransfer] connection=DIRECT", requester.Log);
    }

    [Fact]
    public async Task Transfer_IsChunked_NeverOneWholeFileMessage()
    {
        var (target, requester) = CreatePair(relayed: false);
        var source = CreateFile("src", "big.bin", FileTransferPolicy.ChunkSizeBytes * 5 + 1);

        await target.Transfer.SendFilesAsync([source]);

        var chunks = target.Sent.OfType<FileTransferChunkMessage>().ToList();
        Assert.Equal(6, chunks.Count);
        Assert.All(chunks, c => Assert.True(c.Data.Length <= FileTransferPolicy.ChunkSizeBytes));
        Assert.Equal(Enumerable.Range(0, 6), chunks.Select(c => c.ChunkIndex));
        Assert.All(target.WireSizes, size => Assert.True(size < 262_144, $"message of {size} bytes exceeds SIPSorcery's SCTP max message size"));
        Assert.IsType<FileTransferStartMessage>(target.Sent.First());
        Assert.IsType<FileTransferCompleteMessage>(target.Sent.Last());
        AssertSameFile(source, Assert.Single(Assert.Single(requester.Received)));
    }

    [Fact]
    public async Task EmptyFile_IsTransferred()
    {
        var (target, requester) = CreatePair(relayed: false);
        var source = CreateFile("src", "empty.txt", 0);

        Assert.Equal(FileTransferSendResult.Completed, await target.Transfer.SendFilesAsync([source]));

        AssertSameFile(source, Assert.Single(Assert.Single(requester.Received)));
    }

    [Fact]
    public async Task MultipleFiles_WithTheSameName_ArriveTogether_NamesPreserved()
    {
        var (target, requester) = CreatePair(relayed: false);
        var first = CreateFile("a", "notes.txt", 100);
        var second = CreateFile("b", "notes.txt", 200);
        var third = CreateFile("b", "other.log", 50_000);

        await target.Transfer.SendFilesAsync([first, second, third]);

        var batch = Assert.Single(requester.Received);
        Assert.Equal(3, batch.Count);
        AssertSameFile(first, batch[0]);
        AssertSameFile(second, batch[1]);
        AssertSameFile(third, batch[2]);
    }

    [Fact]
    public async Task Sender_WaitsForBufferToDrain_ThenStillCompletes()
    {
        var polls = 0;
        var (target, requester) = CreatePair(relayed: false, bufferedAmount: () => ++polls < 4 ? FileTransferPolicy.MaxBufferedBytes + 1 : 0);
        var source = CreateFile("src", "buffered.bin", 40_000);

        Assert.Equal(FileTransferSendResult.Completed, await target.Transfer.SendFilesAsync([source]));

        Assert.True(polls >= 4);
        AssertSameFile(source, Assert.Single(Assert.Single(requester.Received)));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Relay_UnderLimit_Succeeds_BothDirections(bool fromTarget)
    {
        var (target, requester) = CreatePair(relayed: true, targetLimitMb: 1, requesterLimitMb: 1);
        var (sender, receiver) = fromTarget ? (target, requester) : (requester, target);
        var source = CreateFile("src", "under.bin", Mb / 2);

        Assert.Equal(FileTransferSendResult.Completed, await sender.Transfer.SendFilesAsync([source]));

        AssertSameFile(source, Assert.Single(Assert.Single(receiver.Received)));
        Assert.Contains("[FileTransfer] connection=RELAY", sender.Log);
        Assert.Contains("[FileTransfer] connection=RELAY", receiver.Log);
        Assert.Empty(requester.Alerts);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Relay_ExactlyAtLimit_Succeeds(bool fromTarget)
    {
        var (target, requester) = CreatePair(relayed: true, targetLimitMb: 1, requesterLimitMb: 1);
        var (sender, receiver) = fromTarget ? (target, requester) : (requester, target);
        var source = CreateFile("src", "exact.bin", Mb);

        Assert.Equal(FileTransferSendResult.Completed, await sender.Transfer.SendFilesAsync([source]));

        AssertSameFile(source, Assert.Single(Assert.Single(receiver.Received)));
    }

    [Fact]
    public async Task Relay_OverLimit_RequesterToTarget_IsRejectedBeforeAnyByteIsSent_UnableToPaste()
    {
        var (target, requester) = CreatePair(relayed: true, targetLimitMb: 1, requesterLimitMb: 1);
        var source = CreateFile("src", "too-big.zip", Mb + 1);

        var result = await requester.Transfer.SendFilesAsync([source]);

        Assert.Equal(FileTransferSendResult.RejectedSizeLimit, result);
        Assert.Empty(requester.Sent);
        Assert.Empty(target.Received);
        var alert = Assert.Single(requester.Alerts);
        Assert.Equal("Unable to paste", alert.Title);
        Assert.Equal("The file exceeds the 1 MB limit for relay connections.", alert.Message);
        Assert.Contains(requester.Log, l => l.StartsWith("[FileTransfer] rejected reason=size_limit"));
    }

    [Fact]
    public async Task Relay_OverLimit_TargetToRequester_IsRejectedBeforeAnyByteIsSent_RequesterSeesUnableToCopy()
    {
        var (target, requester) = CreatePair(relayed: true, targetLimitMb: 1, requesterLimitMb: 1);
        var source = CreateFile("src", "too-big.iso", Mb + 1);

        var result = await target.Transfer.SendFilesAsync([source]);

        Assert.Equal(FileTransferSendResult.RejectedSizeLimit, result);
        var notice = Assert.IsType<FileTransferErrorMessage>(Assert.Single(target.Sent));
        Assert.Equal("size_limit", notice.Reason);
        Assert.Empty(target.Alerts);
        var alert = Assert.Single(requester.Alerts);
        Assert.Equal("Unable to copy", alert.Title);
        Assert.Equal("The file exceeds the 1 MB limit for relay connections.", alert.Message);
        Assert.Empty(requester.Received);
    }

    [Fact]
    public async Task Relay_OneOversizedFileInABatch_RejectsTheWholeBatch()
    {
        var (target, requester) = CreatePair(relayed: true, targetLimitMb: 1, requesterLimitMb: 1);
        var small = CreateFile("src", "small.txt", 10);
        var big = CreateFile("src", "big.bin", Mb + 1);

        Assert.Equal(FileTransferSendResult.RejectedSizeLimit, await requester.Transfer.SendFilesAsync([small, big]));
        Assert.Empty(requester.Sent);
    }

    [Fact]
    public async Task Receiver_RejectsOversizedRelayTransfer_EvenWhenTheSenderAllowedIt()
    {
        var (target, requester) = CreatePair(relayed: true, targetLimitMb: 5, requesterLimitMb: 1);
        var source = CreateFile("src", "2mb.bin", 2 * Mb);

        var result = await target.Transfer.SendFilesAsync([source]);

        Assert.Equal(FileTransferSendResult.RejectedByPeer, result);
        Assert.Empty(requester.Received);
        Assert.Empty(target.Sent.OfType<FileTransferChunkMessage>());
        var error = Assert.IsType<FileTransferErrorMessage>(Assert.Single(requester.Sent));
        Assert.Equal("size_limit", error.Reason);
        var alert = Assert.Single(requester.Alerts);
        Assert.Equal("Unable to copy", alert.Title);
        Assert.Equal("The file exceeds the 1 MB limit for relay connections.", alert.Message);
        Assert.Contains(requester.Log, l => l.StartsWith("[FileTransfer] rejected reason=size_limit"));
        Assert.False(Directory.Exists(requester.StagingRoot)
            && Directory.EnumerateFiles(requester.StagingRoot, "*", SearchOption.AllDirectories).Any());
    }

    [Fact]
    public async Task Receiver_RejectsOversizedPaste_TargetSide_RequesterSeesUnableToPaste()
    {
        var (target, requester) = CreatePair(relayed: true, targetLimitMb: 1, requesterLimitMb: 5);
        var source = CreateFile("src", "2mb.bin", 2 * Mb);

        Assert.Equal(FileTransferSendResult.RejectedByPeer, await requester.Transfer.SendFilesAsync([source]));

        Assert.Empty(target.Received);
        Assert.Empty(target.Alerts);
        var alert = Assert.Single(requester.Alerts);
        Assert.Equal("Unable to paste", alert.Title);
        Assert.Equal("The file exceeds the 1 MB limit for relay connections.", alert.Message);
    }

    [Fact]
    public void Receiver_RejectsCraftedStartDeclaringMoreThanTheLimit()
    {
        var (_, requester) = CreatePair(relayed: true, requesterLimitMb: 1);

        requester.Transfer.Handle(new FileTransferStartMessage("t1", "b1", 0, 1, "evil.bin", Mb + 1L));

        var error = Assert.IsType<FileTransferErrorMessage>(Assert.Single(requester.Sent));
        Assert.Equal("size_limit", error.Reason);
        Assert.False(Directory.Exists(Path.Combine(requester.StagingRoot, "b1")));
    }

    [Fact]
    public void Receiver_RejectsMoreBytesThanDeclared()
    {
        var (_, requester) = CreatePair(relayed: false);

        requester.Transfer.Handle(new FileTransferStartMessage("t1", "b1", 0, 1, "liar.bin", 10));
        requester.Transfer.Handle(new FileTransferChunkMessage("t1", 0, new byte[20]));
        requester.Transfer.Handle(new FileTransferCompleteMessage("t1", 1));

        Assert.Equal("size_mismatch", Assert.IsType<FileTransferErrorMessage>(Assert.Single(requester.Sent)).Reason);
        Assert.Empty(requester.Received);
        Assert.False(Directory.Exists(Path.Combine(requester.StagingRoot, "b1")));
    }

    [Theory]
    [InlineData("..\\escape.txt")]
    [InlineData("sub/dir.txt")]
    [InlineData("..")]
    [InlineData("")]
    public void Receiver_RejectsUnsafeFileNames(string fileName)
    {
        var (_, requester) = CreatePair(relayed: false);

        requester.Transfer.Handle(new FileTransferStartMessage("t1", "b1", 0, 1, fileName, 5));

        Assert.Equal("invalid_file_name", Assert.IsType<FileTransferErrorMessage>(Assert.Single(requester.Sent)).Reason);
    }

    [Fact]
    public void Receiver_RejectsAbsurdFileCount()
    {
        var (_, requester) = CreatePair(relayed: false);

        requester.Transfer.Handle(new FileTransferStartMessage("t1", "b1", 0, int.MaxValue, "a.txt", 5));

        Assert.Equal("invalid_index", Assert.IsType<FileTransferErrorMessage>(Assert.Single(requester.Sent)).Reason);
    }

    [Fact]
    public void Receiver_DiscardsPartialFile_WhenSenderCancels()
    {
        var (_, requester) = CreatePair(relayed: false);

        requester.Transfer.Handle(new FileTransferStartMessage("t1", "b1", 0, 1, "partial.bin", 100));
        requester.Transfer.Handle(new FileTransferChunkMessage("t1", 0, new byte[50]));
        requester.Transfer.Handle(new FileTransferCancelMessage("t1", "cancelled"));

        Assert.Empty(requester.Received);
        Assert.False(Directory.Exists(Path.Combine(requester.StagingRoot, "b1")));
    }

    [Theory]
    [InlineData(FileTransferDirection.TargetToRequester, 20, "Unable to copy", "The file exceeds the 20 MB limit for relay connections.")]
    [InlineData(FileTransferDirection.RequesterToTarget, 20, "Unable to paste", "The file exceeds the 20 MB limit for relay connections.")]
    [InlineData(FileTransferDirection.TargetToRequester, 35, "Unable to copy", "The file exceeds the 35 MB limit for relay connections.")]
    [InlineData(FileTransferDirection.RequesterToTarget, 7, "Unable to paste", "The file exceeds the 7 MB limit for relay connections.")]
    public void SizeLimitAlert_UsesDirectionTitle_AndConfiguredLimit(FileTransferDirection direction, int limitMb, string title, string message)
    {
        var alert = FileTransferPolicy.SizeLimitAlert(direction, limitMb);

        Assert.Equal(title, alert.Title);
        Assert.Equal(message, alert.Message);
    }

    private (Peer Target, Peer Requester) CreatePair(bool relayed, int targetLimitMb = 20, int requesterLimitMb = 20, Func<ulong>? bufferedAmount = null)
    {
        var target = new Peer(Path.Combine(_root, "target-staging"));
        var requester = new Peer(Path.Combine(_root, "requester-staging"));
        target.Transfer = target.Create(isTarget: true, targetLimitMb, relayed, requester, bufferedAmount);
        requester.Transfer = requester.Create(isTarget: false, requesterLimitMb, relayed, target, bufferedAmount);
        return (target, requester);
    }

    private string CreateFile(string folder, string name, int size)
    {
        var directory = Path.Combine(_root, folder);
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, name);
        var bytes = new byte[size];
        new Random(size ^ name.GetHashCode()).NextBytes(bytes);
        File.WriteAllBytes(path, bytes);
        return path;
    }

    private static void AssertSameFile(string expectedPath, string actualPath)
    {
        Assert.Equal(Path.GetFileName(expectedPath), Path.GetFileName(actualPath));
        Assert.Equal(File.ReadAllBytes(expectedPath), File.ReadAllBytes(actualPath));
    }

    private sealed class Peer(string stagingRoot)
    {
        public string StagingRoot { get; } = stagingRoot;
        public ClipboardFileTransfer Transfer { get; set; } = null!;
        public List<ControlMessage> Sent { get; } = [];
        public List<int> WireSizes { get; } = [];
        public List<IReadOnlyList<string>> Received { get; } = [];
        public List<FileTransferAlert> Alerts { get; } = [];
        public List<string> Log { get; } = [];

        public ClipboardFileTransfer Create(bool isTarget, int limitMb, bool relayed, Peer other, Func<ulong>? bufferedAmount)
        {
            var transfer = new ClipboardFileTransfer(
                isTarget,
                limitMb,
                isRelayed: () => relayed,
                send: message =>
                {
                    var json = ControlMessageSerializer.Serialize(message);
                    WireSizes.Add(System.Text.Encoding.UTF8.GetByteCount(json));
                    var decoded = ControlMessageSerializer.TryDeserialize(json) ?? throw new InvalidOperationException($"not a control message: {json}");
                    Sent.Add(decoded);
                    other.Transfer.Handle(decoded);
                },
                bufferedAmount: bufferedAmount ?? (() => 0),
                stagingRoot: StagingRoot,
                log: Log.Add);
            transfer.FilesReceived += Received.Add;
            transfer.AlertRaised += Alerts.Add;
            return transfer;
        }
    }
}
