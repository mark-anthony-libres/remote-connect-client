namespace RemoteDesktopClient.Core.Configuration;

[AttributeUsage(AttributeTargets.Property)]
public sealed class EnvKeyAttribute(string key) : Attribute
{
    public string Key { get; } = key;
}
