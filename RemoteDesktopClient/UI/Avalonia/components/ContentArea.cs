using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using RemoteDesktopClient.Core.Device;
using RemoteDesktopClient.UI.Avalonia.Components.ConnectToRemoteDevicePanel;
using RemoteDesktopClient.UI.Avalonia.Components.RecentConnectionsPanel;
using RemoteDesktopClient.UI.Avalonia.Components.ThisDevicePanel;

namespace RemoteDesktopClient.UI.Avalonia.Components;

internal sealed class ContentArea : ScrollViewer
{
    protected override Type StyleKeyOverride => typeof(ScrollViewer);

    private const double MinContentWidth = 800;

    public ThisDeviceCard ThisDeviceCard { get; }
    public RemoteConnectionCard RemoteConnectionCard { get; }
    public RecentConnectionsCard RecentConnectionsCard { get; }

    public ContentArea(bool enableSessionThumbnails)
    {
        var layout = new Grid
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            MinWidth = MinContentWidth,
            Margin = new Thickness(28, 20, 28, 16),
        };
        layout.RowDefinitions.Add(new RowDefinition(220, GridUnitType.Pixel));
        layout.RowDefinitions.Add(new RowDefinition(198, GridUnitType.Pixel));
        layout.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

        ThisDeviceCard = new ThisDeviceCard();
        Grid.SetRow(ThisDeviceCard, 0);
        ThisDeviceCard.Margin = new Thickness(0, 0, 0, 20);

        RemoteConnectionCard = new RemoteConnectionCard();
        Grid.SetRow(RemoteConnectionCard, 1);
        RemoteConnectionCard.Margin = new Thickness(0, 0, 0, 20);

        RecentConnectionsCard = new RecentConnectionsCard(Array.Empty<DeviceConnection>(), enableSessionThumbnails);
        Grid.SetRow(RecentConnectionsCard, 2);

        layout.Children.Add(ThisDeviceCard);
        layout.Children.Add(RemoteConnectionCard);
        layout.Children.Add(RecentConnectionsCard);

        Content = layout;
    }
}
