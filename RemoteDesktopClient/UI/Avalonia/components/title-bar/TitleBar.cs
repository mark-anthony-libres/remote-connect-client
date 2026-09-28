using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using RemoteDesktopClient.UI.Avalonia;
using RemoteDesktopClient.UI.Avalonia.Components.Shared;

namespace RemoteDesktopClient.UI.Avalonia.Components.TitleBar;

internal sealed class TitleBar : Border
{
    public CaptionButton MinimizeButton { get; }
    public CaptionButton MaximizeButton { get; }
    public CaptionButton CloseButton { get; }

    public event EventHandler<PointerPressedEventArgs>? DragRequested;
    public event EventHandler? ToggleMaximizeRequested;

    public TitleBar()
    {
        this[DockPanel.DockProperty] = Dock.Top;
        Height = 36;
        Background = new SolidColorBrush(AppTheme.CardBackground);
        PointerPressed += (_, e) =>
        {
            if (!ReferenceEquals(e.Source, this))
                return;
            if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
                return;
            if (e.ClickCount == 2)
                ToggleMaximizeRequested?.Invoke(this, EventArgs.Empty);
            else
                DragRequested?.Invoke(this, e);
        };

        MinimizeButton = new CaptionButton { Icon = IconKind.WindowMinimize };
        MaximizeButton = new CaptionButton { Icon = IconKind.WindowMaximize };
        CloseButton = new CaptionButton { Icon = IconKind.WindowClose, IsCloseButton = true };

        var stack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Stretch, HorizontalAlignment = HorizontalAlignment.Right };
        stack.Children.Add(MinimizeButton);
        stack.Children.Add(MaximizeButton);
        stack.Children.Add(CloseButton);
        Child = stack;
    }
}
