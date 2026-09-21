namespace RemoteDesktopClient.Core.Device;

/// <summary>Whether a device is currently reachable.</summary>
public enum ConnectionStatus
{
    Online,
    Offline
}

/// <summary>The kind of device, used by the UI to pick a representative icon
/// and color — kept as a plain enum here so this stays independent of any
/// specific icon/graphics implementation.</summary>
public enum DeviceType
{
    Desktop,
    Home,
    Laptop,
    Server
}

/// <summary>Placeholder data for one entry in the Recent Connections list.</summary>
public sealed class DeviceConnection
{
    public required string DeviceName { get; init; }
    public required string DeviceId { get; init; }
    public required ConnectionStatus Status { get; init; }
    public required string LastConnectedText { get; init; }
    public required DeviceType Type { get; init; }
}
