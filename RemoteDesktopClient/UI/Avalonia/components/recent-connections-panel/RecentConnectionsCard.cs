using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using RemoteDesktopClient.Core.Device;
using RemoteDesktopClient.UI.Avalonia;
using RemoteDesktopClient.UI.Avalonia.Components.Shared;

namespace RemoteDesktopClient.UI.Avalonia.Components.RecentConnectionsPanel;

internal sealed class RecentConnectionsCard : Border
{
    private const double PlaceholderHeight = 140;

    private readonly WrapPanel _cardsFlow;
    private readonly Control _loadingState;
    private readonly TextBlock _emptyState;
    private readonly Control _errorState;
    private readonly bool _enableSessionThumbnails;

    public event EventHandler<string>? ConnectClicked;

    public event EventHandler? RetryClicked;

    public RecentConnectionsCard(IReadOnlyList<DeviceConnection> devices, bool enableSessionThumbnails)
    {
        _enableSessionThumbnails = enableSessionThumbnails;
        Background = new SolidColorBrush(AppTheme.CardBackground);
        BorderBrush = new SolidColorBrush(AppTheme.CardBorder);
        BorderThickness = new Thickness(1);
        CornerRadius = new CornerRadius(12);
        Padding = new Thickness(24);

        var layout = new Grid();
        layout.RowDefinitions.Add(new RowDefinition(58, GridUnitType.Pixel));
        layout.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

        var headerRow = new Grid();
        headerRow.ColumnDefinitions.Add(new ColumnDefinition(48, GridUnitType.Pixel));
        headerRow.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
        headerRow.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        Grid.SetRow(headerRow, 0);

        var iconBadge = new IconBadge
        {
            Icon = IconKind.Clock,
            TintBackground = AppTheme.IconBlueBg,
            TintForeground = AppTheme.IconBlueFg,
            Width = 36,
            Height = 36,
            CornerRadius = 10,
            VerticalAlignment = VerticalAlignment.Top,
        };
        Grid.SetColumn(iconBadge, 0);

        var textStack = new StackPanel();
        textStack.Children.Add(SharedBuilders.TextBlockOf("Recent Connections", AppTheme.FontSectionHeader, AppTheme.TextPrimary));
        textStack.Children.Add(SharedBuilders.TextBlockOf("Quickly connect to your frequently used devices.", AppTheme.FontBody, AppTheme.TextSecondary, new Thickness(0, 4, 0, 0)));
        Grid.SetColumn(textStack, 1);

        var clearAllButton = new ModernButton { Text = "Clear All", Icon = IconKind.Trash, Variant = ButtonVariant.Subtle, Width = 112, Height = 38, VerticalAlignment = VerticalAlignment.Top };
        Grid.SetColumn(clearAllButton, 2);

        headerRow.Children.Add(iconBadge);
        headerRow.Children.Add(textStack);
        headerRow.Children.Add(clearAllButton);

        _cardsFlow = new WrapPanel { Orientation = Orientation.Horizontal, ItemSpacing = 20, LineSpacing = 20 };
        Grid.SetRow(_cardsFlow, 1);

        _loadingState = BuildLoadingState();
        Grid.SetRow(_loadingState, 1);

        _emptyState = SharedBuilders.TextBlockOf("No recent connections", AppTheme.FontBody, AppTheme.TextSecondary);
        _emptyState.HorizontalAlignment = HorizontalAlignment.Center;
        _emptyState.VerticalAlignment = VerticalAlignment.Center;
        _emptyState.Height = PlaceholderHeight;
        Grid.SetRow(_emptyState, 1);

        _errorState = BuildErrorState();
        Grid.SetRow(_errorState, 1);

        layout.Children.Add(headerRow);
        layout.Children.Add(_cardsFlow);
        layout.Children.Add(_loadingState);
        layout.Children.Add(_emptyState);
        layout.Children.Add(_errorState);

        Child = layout;

        if (devices.Count > 0)
            UpdateDevices(devices);
        else
            ShowLoading();
    }

    public void ShowLoading() => SetActiveState(_loadingState);

    public void ShowError() => SetActiveState(_errorState);

    public void UpdateDevices(IReadOnlyList<DeviceConnection> devices)
    {
        if (devices.Count == 0)
        {
            _cardsFlow.Children.Clear();
            SetActiveState(_emptyState);
            return;
        }

        BuildCards(devices);
        SetActiveState(_cardsFlow);
    }

    public void UpdateDeviceStatus(string deviceId, bool isOnline)
    {
        foreach (var child in _cardsFlow.Children)
        {
            if (child is RecentDeviceCard { } card && card.Device.DeviceId == deviceId)
            {
                card.StatusBadge.IsOnline = isOnline;
                card.StatusBadge.InvalidateVisual();
                return;
            }
        }
    }

    public void RefreshDeviceThumbnail(string deviceId)
    {
        foreach (var child in _cardsFlow.Children)
        {
            if (child is RecentDeviceCard { } card && card.Device.DeviceId == deviceId)
            {
                card.RefreshThumbnail();
                return;
            }
        }
    }

    private void SetActiveState(Control active)
    {
        _cardsFlow.IsVisible = ReferenceEquals(active, _cardsFlow);
        _loadingState.IsVisible = ReferenceEquals(active, _loadingState);
        _emptyState.IsVisible = ReferenceEquals(active, _emptyState);
        _errorState.IsVisible = ReferenceEquals(active, _errorState);
    }

    private static Control BuildLoadingState()
    {
        var spinner = new Shared.Spinner
        {
            Width = 32,
            Height = 32,
            Color = AppTheme.Accent,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        return new Border { Child = spinner, Height = PlaceholderHeight };
    }

    private Control BuildErrorState()
    {
        var message = SharedBuilders.TextBlockOf("Unable to load recent connections", AppTheme.FontBody, AppTheme.TextSecondary);
        message.HorizontalAlignment = HorizontalAlignment.Center;

        var retryButton = new ModernButton
        {
            Text = "Retry",
            Variant = ButtonVariant.Subtle,
            Width = 96,
            Height = 36,
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        retryButton.Click += (_, e) => RetryClicked?.Invoke(this, e);

        var stack = new StackPanel
        {
            Orientation = Orientation.Vertical,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Spacing = 12,
        };
        stack.Children.Add(message);
        stack.Children.Add(retryButton);

        return new Border { Child = stack, Height = PlaceholderHeight };
    }

    private void BuildCards(IReadOnlyList<DeviceConnection> devices)
    {
        _cardsFlow.Children.Clear();
        foreach (var device in devices)
        {
            var (icon, background, foreground) = GetIconStyle(device.Type);
            var card = new RecentDeviceCard(device, icon, background, foreground, _enableSessionThumbnails);
            card.ConnectClicked += (sender, _) => ConnectClicked?.Invoke(this, ((RecentDeviceCard)sender!).Device.DeviceId);
            _cardsFlow.Children.Add(card);
        }
    }

    private static (IconKind Icon, Color Background, Color Foreground) GetIconStyle(DeviceType type) => type switch
    {
        DeviceType.Desktop => (IconKind.Monitor, AppTheme.IconBlueBg, AppTheme.IconBlueFg),
        DeviceType.Home => (IconKind.Home, AppTheme.IconPurpleBg, AppTheme.IconPurpleFg),
        DeviceType.Laptop => (IconKind.Laptop, AppTheme.IconGreenBg, AppTheme.IconGreenFg),
        DeviceType.Server => (IconKind.Server, AppTheme.IconOrangeBg, AppTheme.IconOrangeFg),
        _ => (IconKind.Monitor, AppTheme.IconBlueBg, AppTheme.IconBlueFg),
    };
}
