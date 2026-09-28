using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using RemoteDesktopClient.UI.Avalonia.Components.Shared;

namespace RemoteDesktopClient.UI.Avalonia;

internal sealed class ActiveSessionToolbar : Window
{
    private const double IdleOpacity = 0.35;
    private const double HoveredOpacity = 1.0;

    public event EventHandler? EndSessionRequested;

    public ActiveSessionToolbar(string statusText)
    {
        WindowDecorations = WindowDecorations.None;
        CanResize = false;
        ShowInTaskbar = false;
        SizeToContent = SizeToContent.WidthAndHeight;
        TransparencyLevelHint = [WindowTransparencyLevel.Transparent];
        Background = Brushes.Transparent;
        Topmost = true;

        Opacity = IdleOpacity;
        Transitions = new Transitions
        {
            new DoubleTransition { Property = OpacityProperty, Duration = TimeSpan.FromMilliseconds(120) },
        };

        var dot = new Ellipse
        {
            Width = 8,
            Height = 8,
            Fill = new SolidColorBrush(AppTheme.Online),
            VerticalAlignment = VerticalAlignment.Center,
        };

        var label = TextBlockOf(statusText);
        label.VerticalAlignment = VerticalAlignment.Center;

        var endButton = new ModernButton { Text = "End Session", Variant = ButtonVariant.Subtle, Width = 108, Height = 28 };
        endButton.Click += (_, _) => EndSessionRequested?.Invoke(this, EventArgs.Empty);

        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 10,
            Margin = new Thickness(16, 8),
            VerticalAlignment = VerticalAlignment.Center,
        };
        row.Children.Add(dot);
        row.Children.Add(label);
        row.Children.Add(endButton);

        var background = new Border
        {
            Background = new SolidColorBrush(AppTheme.CardBackground),
            BorderBrush = new SolidColorBrush(AppTheme.CardBorder),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(18),
            Child = row,
        };
        Content = background;

        background.PointerEntered += (_, _) => Opacity = HoveredOpacity;
        background.PointerExited += (_, _) => Opacity = IdleOpacity;

        WireDragging(background);

        Opened += (_, _) => CenterNearTopOfPrimaryScreen();
    }

    private void WireDragging(Border background)
    {
        PixelPoint? dragStartPointerScreen = null;
        PixelPoint? dragStartWindowPosition = null;

        background.PointerPressed += (_, e) =>
        {
            if (!ReferenceEquals(e.Source, background))
                return;
            if (!e.GetCurrentPoint(background).Properties.IsLeftButtonPressed)
                return;

            e.Pointer.Capture(background);
            dragStartPointerScreen = background.PointToScreen(e.GetPosition(background));
            dragStartWindowPosition = Position;
        };

        background.PointerMoved += (_, e) =>
        {
            if (dragStartPointerScreen is not { } startPointer || dragStartWindowPosition is not { } startWindow)
                return;

            var currentScreen = background.PointToScreen(e.GetPosition(background));
            var candidate = new PixelPoint(
                startWindow.X + (currentScreen.X - startPointer.X),
                startWindow.Y + (currentScreen.Y - startPointer.Y));
            Position = ClampToWorkingArea(candidate);
        };

        background.PointerReleased += (_, e) =>
        {
            dragStartPointerScreen = null;
            dragStartWindowPosition = null;
            e.Pointer.Capture(null);
        };
    }

    private PixelPoint ClampToWorkingArea(PixelPoint candidate)
    {
        var workingArea = Screens.ScreenFromPoint(candidate)?.WorkingArea
            ?? Screens.ScreenFromWindow(this)?.WorkingArea
            ?? Screens.Primary?.WorkingArea;
        if (workingArea is not { } area)
            return candidate;

        var toolbarWidth = (int)Math.Ceiling(Bounds.Width * RenderScaling);
        var toolbarHeight = (int)Math.Ceiling(Bounds.Height * RenderScaling);

        var maxX = Math.Max(area.X, area.Right - toolbarWidth);
        var maxY = Math.Max(area.Y, area.Bottom - toolbarHeight);

        return new PixelPoint(
            Math.Clamp(candidate.X, area.X, maxX),
            Math.Clamp(candidate.Y, area.Y, maxY));
    }

    private void CenterNearTopOfPrimaryScreen()
    {
        if (Screens.Primary?.WorkingArea is not { } area)
            return;

        var toolbarWidth = (int)Math.Ceiling(Bounds.Width * RenderScaling);
        Position = new PixelPoint(area.X + (area.Width - toolbarWidth) / 2, area.Y + 24);
    }

    private static TextBlock TextBlockOf(string text) => new()
    {
        Text = text,
        FontFamily = AppTheme.FontBody.Family,
        FontSize = AppTheme.FontBody.Size,
        Foreground = new SolidColorBrush(AppTheme.TextPrimary),
    };
}
