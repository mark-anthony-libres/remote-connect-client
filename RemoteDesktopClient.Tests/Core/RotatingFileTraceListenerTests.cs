using RemoteDesktopClient.Core;

namespace RemoteDesktopClient.Tests.Core;

public sealed class RotatingFileTraceListenerTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "RemoteConnectTests", Guid.NewGuid().ToString("N"));

    public RotatingFileTraceListenerTests() => Directory.CreateDirectory(_directory);

    public void Dispose()
    {
        try { Directory.Delete(_directory, recursive: true); } catch (IOException) { }
    }

    [Fact]
    public void FirstApplicationStart_CreatesBase1Part1()
    {
        using (var listener = new RotatingFileTraceListener(_directory, maxFileSizeMb: 10, maxFiles: 10))
            listener.WriteLine("hello");

        Assert.True(File.Exists(RotatingFileTraceListener.BuildPartPath(_directory, 1, 1)));
        Assert.Equal("hello" + Environment.NewLine, File.ReadAllText(RotatingFileTraceListener.BuildPartPath(_directory, 1, 1)));
    }

    [Fact]
    public void ApplicationRestart_CreatesANewBase_NotANewPartOfTheOldOne()
    {
        using (var first = new RotatingFileTraceListener(_directory, maxFileSizeMb: 10, maxFiles: 10))
            first.WriteLine("first run");

        using (var second = new RotatingFileTraceListener(_directory, maxFileSizeMb: 10, maxFiles: 10))
            second.WriteLine("second run");

        Assert.True(File.Exists(RotatingFileTraceListener.BuildPartPath(_directory, 1, 1)));
        Assert.True(File.Exists(RotatingFileTraceListener.BuildPartPath(_directory, 2, 1)));
        Assert.Equal("first run" + Environment.NewLine, File.ReadAllText(RotatingFileTraceListener.BuildPartPath(_directory, 1, 1)));
        Assert.Equal("second run" + Environment.NewLine, File.ReadAllText(RotatingFileTraceListener.BuildPartPath(_directory, 2, 1)));
    }

    [Fact]
    public void ThirdRestart_ContinuesIncrementing_RegardlessOfGapsOrOrder()
    {
        for (var run = 1; run <= 3; run++)
        {
            using var listener = new RotatingFileTraceListener(_directory, maxFileSizeMb: 10, maxFiles: 10);
            listener.WriteLine($"run {run}");
        }

        Assert.True(File.Exists(RotatingFileTraceListener.BuildPartPath(_directory, 1, 1)));
        Assert.True(File.Exists(RotatingFileTraceListener.BuildPartPath(_directory, 2, 1)));
        Assert.True(File.Exists(RotatingFileTraceListener.BuildPartPath(_directory, 3, 1)));
    }

    [Fact]
    public void WritingPastMaxFileSize_RotatesToANewPart_SameBase()
    {
        using (var listener = new RotatingFileTraceListener(_directory, maxFileSizeMb: 1, maxFiles: 10))
        {
            var chunk = new string('x', 1024 * 1024);
            listener.WriteLine(chunk);
            listener.WriteLine("still part 1 or already part 2?");
            listener.WriteLine("this one must land in part 2");
        }

        Assert.True(File.Exists(RotatingFileTraceListener.BuildPartPath(_directory, 1, 1)));
        Assert.True(File.Exists(RotatingFileTraceListener.BuildPartPath(_directory, 1, 2)));
        Assert.False(File.Exists(RotatingFileTraceListener.BuildPartPath(_directory, 1, 3)));
        Assert.Contains("this one must land in part 2", File.ReadAllText(RotatingFileTraceListener.BuildPartPath(_directory, 1, 2)));
    }

    [Fact]
    public void RotationNeverSplitsASingleWrite_AcrossTwoFiles()
    {
        var chunk = new string('y', 1024 * 1024 + 500);
        using (var listener = new RotatingFileTraceListener(_directory, maxFileSizeMb: 1, maxFiles: 10))
            listener.WriteLine(chunk);

        var part1 = File.ReadAllText(RotatingFileTraceListener.BuildPartPath(_directory, 1, 1));
        Assert.Equal(chunk + Environment.NewLine, part1);
        Assert.Equal(string.Empty, File.ReadAllText(RotatingFileTraceListener.BuildPartPath(_directory, 1, 2)));
    }

    [Fact]
    public void Retention_KeepsOnlyTheLatestConfiguredNumberOfBases()
    {
        for (var run = 1; run <= 3; run++)
        {
            using var listener = new RotatingFileTraceListener(_directory, maxFileSizeMb: 10, maxFiles: 2);
            listener.WriteLine($"run {run}");
        }

        Assert.False(File.Exists(RotatingFileTraceListener.BuildPartPath(_directory, 1, 1)));
        Assert.True(File.Exists(RotatingFileTraceListener.BuildPartPath(_directory, 2, 1)));
        Assert.True(File.Exists(RotatingFileTraceListener.BuildPartPath(_directory, 3, 1)));
    }

    [Fact]
    public void Retention_DeletesEveryPartFile_OfTheOldestBase_NotJustPart1()
    {
        using (var first = new RotatingFileTraceListener(_directory, maxFileSizeMb: 1, maxFiles: 2))
        {
            var chunk = new string('z', 1024 * 1024);
            first.WriteLine(chunk);
            first.WriteLine(chunk);
        }
        Assert.True(File.Exists(RotatingFileTraceListener.BuildPartPath(_directory, 1, 3)));

        using (var second = new RotatingFileTraceListener(_directory, maxFileSizeMb: 1, maxFiles: 2))
            second.WriteLine("run 2");
        using (var third = new RotatingFileTraceListener(_directory, maxFileSizeMb: 1, maxFiles: 2))
            third.WriteLine("run 3");

        Assert.False(File.Exists(RotatingFileTraceListener.BuildPartPath(_directory, 1, 1)));
        Assert.False(File.Exists(RotatingFileTraceListener.BuildPartPath(_directory, 1, 2)));
        Assert.False(File.Exists(RotatingFileTraceListener.BuildPartPath(_directory, 1, 3)));
        Assert.True(File.Exists(RotatingFileTraceListener.BuildPartPath(_directory, 2, 1)));
        Assert.True(File.Exists(RotatingFileTraceListener.BuildPartPath(_directory, 3, 1)));
    }

    [Fact]
    public void Retention_NeverDeletesTheNewBaseItselfOrAnyOtherRecentOne()
    {
        for (var run = 1; run <= 5; run++)
        {
            using var listener = new RotatingFileTraceListener(_directory, maxFileSizeMb: 10, maxFiles: 3);
            listener.WriteLine($"run {run}");
        }

        for (var oldBase = 1; oldBase <= 2; oldBase++)
            Assert.False(File.Exists(RotatingFileTraceListener.BuildPartPath(_directory, oldBase, 1)));
        for (var keptBase = 3; keptBase <= 5; keptBase++)
            Assert.True(File.Exists(RotatingFileTraceListener.BuildPartPath(_directory, keptBase, 1)));
    }

    [Fact]
    public void NextBaseNumber_IsOne_WhenDirectoryIsEmpty()
    {
        Assert.Equal(1, RotatingFileTraceListener.NextBaseNumber(_directory));
    }

    [Fact]
    public void NextBaseNumber_IgnoresUnrelatedFiles()
    {
        File.WriteAllText(Path.Combine(_directory, "client.log"), "old, unrotated file - not client-{base}-{part}.log");
        File.WriteAllText(Path.Combine(_directory, "client-5-2.log"), "x");
        File.WriteAllText(Path.Combine(_directory, "not-a-log-file.txt"), "x");

        Assert.Equal(6, RotatingFileTraceListener.NextBaseNumber(_directory));
    }

    [Theory]
    [InlineData("client-1-1.log", true, 1)]
    [InlineData("client-42-7.log", true, 42)]
    [InlineData("client.log", false, 0)]
    [InlineData("client-1.log", false, 0)]
    [InlineData("client-a-1.log", false, 0)]
    public void TryParseBaseNumber_MatchesOnlyTheExactPattern(string fileName, bool expectedSuccess, int expectedBase)
    {
        var success = RotatingFileTraceListener.TryParseBaseNumber(fileName, out var baseNumber);

        Assert.Equal(expectedSuccess, success);
        Assert.Equal(expectedBase, baseNumber);
    }
}
