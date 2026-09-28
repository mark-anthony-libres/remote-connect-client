namespace RemoteDesktopClient.Core.Device;

internal static class RelativeTimeFormatter
{
    public static string FormatLastConnected(DateTimeOffset timestamp)
    {
        var elapsed = DateTimeOffset.UtcNow - timestamp;
        if (elapsed < TimeSpan.Zero)
            elapsed = TimeSpan.Zero;

        string relative = elapsed switch
        {
            { TotalSeconds: < 60 } => "just now",
            { TotalMinutes: < 2 } => "1 minute ago",
            { TotalMinutes: < 60 } => $"{(int)elapsed.TotalMinutes} minutes ago",
            { TotalHours: < 2 } => "1 hour ago",
            { TotalHours: < 24 } => $"{(int)elapsed.TotalHours} hours ago",
            { TotalDays: < 2 } => "1 day ago",
            { TotalDays: < 30 } => $"{(int)elapsed.TotalDays} days ago",
            { TotalDays: < 60 } => "1 month ago",
            { TotalDays: < 365 } => $"{(int)(elapsed.TotalDays / 30)} months ago",
            _ => $"{(int)(elapsed.TotalDays / 365)} year(s) ago",
        };
        return relative == "just now" ? "Last connected: just now" : $"Last connected: {relative}";
    }
}
