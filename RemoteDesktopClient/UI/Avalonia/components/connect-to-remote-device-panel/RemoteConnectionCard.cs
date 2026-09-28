using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using RemoteDesktopClient.UI.Avalonia;
using RemoteDesktopClient.UI.Avalonia.Components.Shared;

namespace RemoteDesktopClient.UI.Avalonia.Components.ConnectToRemoteDevicePanel;

internal sealed class RemoteConnectionCard : Border
{
    public IconTextBox RemoteIdInput { get; }
    public ModernButton ConnectButton { get; }

    public RemoteConnectionCard()
    {
        Background = new SolidColorBrush(AppTheme.CardBackground);
        BorderBrush = new SolidColorBrush(AppTheme.CardBorder);
        BorderThickness = new Thickness(1);
        CornerRadius = new CornerRadius(12);
        Padding = new Thickness(24);

        var layout = new Grid();
        layout.RowDefinitions.Add(new RowDefinition(58, GridUnitType.Pixel));
        layout.RowDefinitions.Add(new RowDefinition(1, GridUnitType.Star));

        var headerRow = new Grid();
        headerRow.ColumnDefinitions.Add(new ColumnDefinition(48, GridUnitType.Pixel));
        headerRow.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
        Grid.SetRow(headerRow, 0);

        var iconBadge = new IconBadge
        {
            Icon = IconKind.Link,
            TintBackground = AppTheme.IconBlueBg,
            TintForeground = AppTheme.IconBlueFg,
            Width = 36,
            Height = 36,
            CornerRadius = 10,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        Grid.SetColumn(iconBadge, 0);

        var headerTextStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        headerTextStack.Children.Add(SharedBuilders.TextBlockOf("Connect to a Remote Device", AppTheme.FontSectionHeader, AppTheme.TextPrimary, new Thickness(0, 0, 0, 4)));
        headerTextStack.Children.Add(SharedBuilders.TextBlockOf("Enter the remote device's ID to establish a connection.", AppTheme.FontBody, AppTheme.TextSecondary));
        Grid.SetColumn(headerTextStack, 1);

        headerRow.Children.Add(iconBadge);
        headerRow.Children.Add(headerTextStack);

        var inputRow = new Grid { VerticalAlignment = VerticalAlignment.Center };
        inputRow.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
        inputRow.ColumnDefinitions.Add(new ColumnDefinition(160, GridUnitType.Pixel));
        Grid.SetRow(inputRow, 1);

        RemoteIdInput = new IconTextBox
        {
            Icon = IconKind.Monitor,
            PlaceholderText = "Enter device ID (e.g. 123 456 789)",
            Margin = new Thickness(0, 4, 24, 4),
        };
        RemoteIdInput.InnerTextBox.FontFamily = AppTheme.FontMonoId.Family;
        RemoteIdInput.InnerTextBox.FontSize = AppTheme.FontMonoId.Size;
        RemoteIdInput.InnerTextBox.FontWeight = AppTheme.FontMonoId.Weight;
        RemoteIdInput.InnerTextBox.Foreground = new SolidColorBrush(AppTheme.Accent);
        Grid.SetColumn(RemoteIdInput, 0);

        ConnectButton = new ModernButton
        {
            Text = "Connect",
            Icon = IconKind.Send,
            Variant = ButtonVariant.Primary,
            Width = 160,
            Height = 52,
            IsEnabled = false,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        Grid.SetColumn(ConnectButton, 1);

        inputRow.Children.Add(RemoteIdInput);
        inputRow.Children.Add(ConnectButton);

        layout.Children.Add(headerRow);
        layout.Children.Add(inputRow);

        Child = layout;
    }
}
