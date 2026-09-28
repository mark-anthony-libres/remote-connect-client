using RemoteDesktopClient.WebRTC;

namespace RemoteDesktopClient.Tests.WebRTC;

public sealed class DebugSessionExporterTests : IDisposable
{
    private readonly string _sourceRoot = Path.Combine(Path.GetTempPath(), "RemoteConnectTests", Guid.NewGuid().ToString("N"));
    private readonly List<string> _createdFolders = [];

    public DebugSessionExporterTests() => Directory.CreateDirectory(_sourceRoot);

    public void Dispose()
    {
        try { Directory.Delete(_sourceRoot, recursive: true); } catch (IOException) { }
        foreach (var folder in _createdFolders)
        {
            try { Directory.Delete(folder, recursive: true); } catch (IOException) { }
        }
    }

    [Fact]
    public void BothLogsPresent_WritesBothFiles_SourceUntouched()
    {
        var requestId = UniqueRequestId();
        var ownLogPath = CreateSourceLog(requestId, "SESSION_STARTED\nrequest_id=abc\n");
        var targetBytes = "SESSION_STARTED\nrequest_id=abc\nrole=TARGET\n"u8.ToArray();

        var folder = Export(requestId, ownLogPath, targetBytes);

        Assert.Equal(File.ReadAllBytes(ownLogPath), File.ReadAllBytes(Path.Combine(folder, "requester.log")));
        Assert.Equal(targetBytes, File.ReadAllBytes(Path.Combine(folder, "target.log")));
        Assert.True(File.Exists(ownLogPath));
        Assert.Equal("SESSION_STARTED\nrequest_id=abc\n", File.ReadAllText(ownLogPath));
    }

    [Fact]
    public void TargetLogUnavailable_OnlyRequesterLogWritten_NeverFabricated()
    {
        var requestId = UniqueRequestId();
        var ownLogPath = CreateSourceLog(requestId, "SESSION_STARTED\n");

        var folder = Export(requestId, ownLogPath, targetLogBytes: null);

        Assert.True(File.Exists(Path.Combine(folder, "requester.log")));
        Assert.False(File.Exists(Path.Combine(folder, "target.log")));
    }

    [Fact]
    public void MissingOwnLogFile_DoesNotThrow_AndStillHandlesTargetLog()
    {
        var requestId = UniqueRequestId();
        var missingPath = Path.Combine(_sourceRoot, "does-not-exist.txt");
        var targetBytes = "target data"u8.ToArray();

        string folder = null!;
        var exception = Record.Exception(() => folder = Export(requestId, missingPath, targetBytes));

        Assert.Null(exception);
        Assert.False(File.Exists(Path.Combine(folder, "requester.log")));
        Assert.Equal(targetBytes, File.ReadAllBytes(Path.Combine(folder, "target.log")));
    }

    [Fact]
    public void BuildFolderPath_SanitizesRequestId_MatchingRemoteSessionTelemetry()
    {
        const string unsafeRequestId = "../../evil";

        var folder = DebugSessionExporter.BuildFolderPath(unsafeRequestId);

        Assert.DoesNotContain("..", Path.GetFileName(folder));
        Assert.Equal(RemoteSessionTelemetry.SanitizeRequestId(unsafeRequestId), Path.GetFileName(folder));
    }

    private static string UniqueRequestId() => $"test-debug-session-{Guid.NewGuid():N}";

    private string Export(string requestId, string ownLogFilePath, byte[]? targetLogBytes)
    {
        DebugSessionExporter.Export(requestId, ownLogFilePath, targetLogBytes);
        var folder = DebugSessionExporter.BuildFolderPath(requestId);
        _createdFolders.Add(folder);
        return folder;
    }

    private string CreateSourceLog(string requestId, string content)
    {
        var path = Path.Combine(_sourceRoot, $"{requestId}.txt");
        File.WriteAllText(path, content);
        return path;
    }
}
