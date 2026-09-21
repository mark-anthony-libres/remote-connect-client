using System.ComponentModel;

namespace RemoteDesktopClient.UI.WinForms.Controls;

/// <summary>A small icon followed by a line of text — e.g. a clock icon next to
/// "Last connected: 2 hours ago".</summary>
internal sealed class IconLabel : Control
{
    private string _text = string.Empty;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public IconKind Icon { get; set; } = IconKind.Clock;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color IconColor { get; set; } = Theme.TextSecondary;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string Caption
    {
        get => _text;
        set { _text = value; Invalidate(); }
    }

    public IconLabel()
    {
        DoubleBuffered = true;
        Font = Theme.FontSmall;
        ForeColor = Theme.TextSecondary;
        Height = 16;
    }

    public override Size GetPreferredSize(Size proposedSize) => Size;

    protected override void OnPaint(PaintEventArgs e)
    {
        var iconBounds = new RectangleF(0, (Height - 12) / 2f, 12, 12);
        Icons.Draw(e.Graphics, Icon, iconBounds, IconColor, 1.3f);

        var textRect = new Rectangle(18, 0, Width - 18, Height);
        TextRenderer.DrawText(
            e.Graphics, _text, Font, textRect, ForeColor,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
    }
}
