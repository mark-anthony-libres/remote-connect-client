using System.Diagnostics;

namespace RemoteDesktopClient.Core.Configuration;

internal static class EnvFile
{
    public static void Load()
    {
        var path = CandidatePaths().FirstOrDefault(File.Exists);
        if (path is null)
            return;

        try
        {
            foreach (var rawLine in File.ReadAllLines(path))
            {
                var line = rawLine.Trim();
                if (line.Length == 0 || line.StartsWith('#'))
                    continue;

                int separator = line.IndexOf('=');
                if (separator <= 0)
                    continue;

                var key = line[..separator].Trim();
                var value = line[(separator + 1)..].Trim().Trim('"');

                if (Environment.GetEnvironmentVariable(key) is null)
                    Environment.SetEnvironmentVariable(key, value);
            }
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"EnvFile: failed to read {path}. {ex}");
        }
    }

    private static IEnumerable<string> CandidatePaths()
    {
        yield return Path.Combine(Directory.GetCurrentDirectory(), ".env");
        yield return Path.Combine(AppContext.BaseDirectory, ".env");
    }
}
