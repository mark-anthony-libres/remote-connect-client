using System.Diagnostics;
using System.Text.RegularExpressions;

namespace RemoteDesktopClient.Core;

internal sealed class RotatingFileTraceListener : TraceListener
{
    private static readonly Regex FileNamePattern = new(@"^client-(?<base>\d+)-(?<part>\d+)\.log$", RegexOptions.Compiled);

    private readonly object _lock = new();
    private readonly string _directory;
    private readonly long _maxFileSizeBytes;
    private readonly int _baseNumber;

    private int _partNumber;
    private FileStream _fileStream;
    private StreamWriter _writer;

    public RotatingFileTraceListener(string directory, int maxFileSizeMb, int maxFiles)
    {
        _directory = directory;
        _maxFileSizeBytes = (long)maxFileSizeMb * 1024 * 1024;

        Directory.CreateDirectory(directory);
        _baseNumber = NextBaseNumber(directory);
        EnforceRetention(directory, _baseNumber, maxFiles);

        _partNumber = 1;
        (_fileStream, _writer) = OpenPart(directory, _baseNumber, _partNumber);
    }

    public override void Write(string? message) => WriteInternal(message);

    public override void WriteLine(string? message) => WriteInternal(message + Environment.NewLine);

    public override void Flush()
    {
        lock (_lock)
            _writer.Flush();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            lock (_lock)
                _writer.Dispose();
        }
        base.Dispose(disposing);
    }

    private void WriteInternal(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return;
        lock (_lock)
        {
            _writer.Write(text);
            _writer.Flush();
            if (_fileStream.Length >= _maxFileSizeBytes)
                RotatePart();
        }
    }

    private void RotatePart()
    {
        _writer.Dispose();
        _partNumber++;
        (_fileStream, _writer) = OpenPart(_directory, _baseNumber, _partNumber);
    }

    private static (FileStream, StreamWriter) OpenPart(string directory, int baseNumber, int partNumber)
    {
        var path = BuildPartPath(directory, baseNumber, partNumber);
        var stream = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read);
        var writer = new StreamWriter(stream) { AutoFlush = true };
        return (stream, writer);
    }

    internal static string BuildPartPath(string directory, int baseNumber, int partNumber) =>
        Path.Combine(directory, $"client-{baseNumber}-{partNumber}.log");

    internal static bool TryParseBaseNumber(string fileName, out int baseNumber)
    {
        var match = FileNamePattern.Match(fileName);
        if (match.Success && int.TryParse(match.Groups["base"].Value, out baseNumber))
            return true;
        baseNumber = 0;
        return false;
    }

    internal static int NextBaseNumber(string directory)
    {
        var maxBase = 0;
        foreach (var file in Directory.EnumerateFiles(directory, "client-*-*.log"))
        {
            if (TryParseBaseNumber(Path.GetFileName(file), out var baseNumber))
                maxBase = Math.Max(maxBase, baseNumber);
        }
        return maxBase + 1;
    }

    internal static void EnforceRetention(string directory, int newBaseNumber, int maxFiles)
    {
        var bases = new SortedSet<int> { newBaseNumber };
        foreach (var file in Directory.EnumerateFiles(directory, "client-*-*.log"))
        {
            if (TryParseBaseNumber(Path.GetFileName(file), out var baseNumber))
                bases.Add(baseNumber);
        }

        var excess = bases.Count - maxFiles;
        if (excess <= 0)
            return;

        foreach (var oldBase in bases.Take(excess))
        {
            foreach (var file in Directory.EnumerateFiles(directory, "client-*-*.log"))
            {
                if (TryParseBaseNumber(Path.GetFileName(file), out var baseNumber) && baseNumber == oldBase)
                {
                    try { File.Delete(file); }
                    catch (IOException) { }
                    catch (UnauthorizedAccessException) { }
                }
            }
        }
    }
}
