using Avalonia;
using Avalonia.Media;

namespace RemoteDesktopClient.UI.Avalonia.Controls;

internal enum TextAlign { Left, Center }

/// <summary>Shared FormattedText helpers so every custom control's Render() doesn't
/// repeat the same Typeface/FormattedText construction boilerplate.</summary>
internal static class TextRendering
{
    public static FormattedText Format(string text, AppFont font, Color color) => new(
        text ?? string.Empty,
        System.Globalization.CultureInfo.CurrentCulture,
        FlowDirection.LeftToRight,
        new Typeface(font.Family, font.Style, font.Weight),
        font.Size,
        new SolidColorBrush(color));

    /// <summary>Draws text left-aligned and vertically centered within <paramref name="bounds"/>,
    /// matching WinForms' TextFormatFlags.Left | TextFormatFlags.VerticalCenter. Single-line
    /// content is ellipsis-trimmed to bounds.Width by default, matching AutoEllipsis/EndEllipsis
    /// on the original WinForms labels.</summary>
    public static void DrawVerticalCenter(DrawingContext context, string text, AppFont font, Color color, Rect bounds, TextAlign align = TextAlign.Left, bool trim = true)
    {
        var formatted = Format(text, font, color);
        formatted.MaxTextWidth = Math.Max(1, bounds.Width);
        if (trim)
        {
            formatted.MaxLineCount = 1;
            formatted.Trimming = TextTrimming.CharacterEllipsis;
        }
        double x = align == TextAlign.Center ? bounds.Left + (bounds.Width - formatted.WidthIncludingTrailingWhitespace) / 2 : bounds.Left;
        double y = bounds.Top + (bounds.Height - formatted.Height) / 2;
        context.DrawText(formatted, new Point(x, y));
    }
}
