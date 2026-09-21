using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace RemoteDesktopClient.UI.Avalonia.Controls;

/// <summary>A bordered input box with a leading icon, wrapping a real, borderless
/// <see cref="TextBox"/> so typing, focus and placeholder text all behave natively.</summary>
internal sealed class IconTextBox : Panel
{
    public TextBox InnerTextBox { get; }

    public IconKind Icon { get; set; } = IconKind.Monitor;

    public string PlaceholderText
    {
        get => InnerTextBox.PlaceholderText ?? string.Empty;
        set => InnerTextBox.PlaceholderText = value;
    }

    public string Text
    {
        get => InnerTextBox.Text ?? string.Empty;
        set => InnerTextBox.Text = value;
    }

    public event EventHandler? TextChanged;

    private readonly Chrome _chrome;

    public IconTextBox()
    {
        InnerTextBox = new TextBox
        {
            BorderThickness = new Thickness(0),
            Background = Brushes.Transparent,
            FontFamily = AppTheme.FontBody.Family,
            FontSize = AppTheme.FontBody.Size,
            VerticalContentAlignment = VerticalAlignment.Center,
            Padding = new Thickness(0),
        };
        InnerTextBox.TextChanged += (_, _) => TextChanged?.Invoke(this, EventArgs.Empty);

        _chrome = new Chrome(this);

        Height = 48;
        // Chrome (rounded background + icon) is added first so it paints behind
        // the real, interactive TextBox — Panel.Render is sealed in Avalonia, so
        // this class can't override Render() itself (see RoundedBackground.cs).
        Children.Add(_chrome);
        Children.Add(InnerTextBox);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        const double leftInset = 40;
        const double rightInset = 12;
        _chrome.Arrange(new Rect(0, 0, finalSize.Width, finalSize.Height));
        InnerTextBox.Arrange(new Rect(leftInset, 0, Math.Max(0, finalSize.Width - leftInset - rightInset), finalSize.Height));
        return finalSize;
    }

    private sealed class Chrome(IconTextBox owner) : Control
    {
        public override void Render(DrawingContext context)
        {
            var rect = new Rect(0, 0, Bounds.Width, Bounds.Height);
            context.DrawRectangle(new SolidColorBrush(AppTheme.CardBackground), new Pen(new SolidColorBrush(AppTheme.CardBorder), 1), new RoundedRect(rect, 10));

            var iconBounds = new Rect(14, Bounds.Height / 2 - 8, 16, 16);
            Icons.Draw(context, owner.Icon, iconBounds, AppTheme.TextSecondary, 1.5);
        }
    }
}
