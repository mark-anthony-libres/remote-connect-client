using RemoteDesktopClient.Core.Device;
using RemoteDesktopClient.UI.WinForms.Controls;

namespace RemoteDesktopClient.UI.WinForms;

public sealed partial class MainForm : Form
{
    private IconTextBox _remoteIdInput = null!;
    private ModernButton _remoteConnectButton = null!;

    public MainForm()
    {
        InitializeComponent();
    }

    // Replaces Form.AcceptButton (see the comment in InitializeComponent):
    // pressing Enter anywhere in the form still triggers Connect, without
    // marking the button as the native "default push button".
    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == Keys.Enter && _remoteConnectButton.Enabled)
        {
            _remoteConnectButton.PerformClick();
            return true;
        }

        return base.ProcessCmdKey(ref msg, keyData);
    }

    // DeviceConnection (Core) only knows its DeviceType, not how that type is
    // drawn — the icon glyph and tint colors are a UI concern, resolved here.
    private static (IconKind Icon, Color Background, Color Foreground) GetIconStyle(DeviceType type) => type switch
    {
        DeviceType.Desktop => (IconKind.Monitor, Theme.IconBlueBg, Theme.IconBlueFg),
        DeviceType.Home => (IconKind.Home, Theme.IconPurpleBg, Theme.IconPurpleFg),
        DeviceType.Laptop => (IconKind.Laptop, Theme.IconGreenBg, Theme.IconGreenFg),
        DeviceType.Server => (IconKind.Server, Theme.IconOrangeBg, Theme.IconOrangeFg),
        _ => (IconKind.Monitor, Theme.IconBlueBg, Theme.IconBlueFg),
    };

    private static IReadOnlyList<DeviceConnection> CreateMockRecentConnections() => new List<DeviceConnection>
    {
        new()
        {
            DeviceName = "Office Desktop — Marketing Team",
            DeviceId = "118 204 552",
            Status = ConnectionStatus.Online,
            LastConnectedText = "Last connected: 2 hours ago",
            Type = DeviceType.Desktop,
        },
        new()
        {
            DeviceName = "Home PC",
            DeviceId = "902 447 310",
            Status = ConnectionStatus.Offline,
            LastConnectedText = "Last connected: 1 day ago",
            Type = DeviceType.Home,
        },
        new()
        {
            DeviceName = "Laptop — Sarah",
            DeviceId = "573 810 226",
            Status = ConnectionStatus.Online,
            LastConnectedText = "Last connected: 3 hours ago",
            Type = DeviceType.Laptop,
        },
        new()
        {
            DeviceName = "Server Room Workstation 4",
            DeviceId = "301 665 918",
            Status = ConnectionStatus.Offline,
            LastConnectedText = "Last connected: 5 days ago",
            Type = DeviceType.Server,
        },
    };
}
