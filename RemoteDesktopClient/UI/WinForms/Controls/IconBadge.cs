using System.ComponentModel;
using System.Drawing.Drawing2D;

namespace RemoteDesktopClient.UI.WinForms.Controls;

/// <summary>A rounded, tint-colored square with a centered icon — used throughout for
/// section markers and per-device icons.</summary>
internal sealed class IconBadge : Control
{
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public IconKind Icon { get; set; } = IconKind.Monitor;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color TintBackground { get; set; } = Theme.IconBlueBg;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color TintForeground { get; set; } = Theme.IconBlueFg;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int CornerRadius { get; set; } = 12;

    public IconBadge()
    {
        DoubleBuffered = true;
        Size = new Size(40, 40);
    }

    public override Size GetPreferredSize(Size proposedSize) => Size;

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

        var rect = new Rectangle(0, 0, Width - 1, Height - 1);
        using var path = RoundedRect.CreatePath(rect, CornerRadius);
        using var fill = new SolidBrush(TintBackground);
        e.Graphics.FillPath(fill, path);

        var iconBounds = new RectangleF(Width * 0.26f, Height * 0.26f, Width * 0.48f, Height * 0.48f);
        Icons.Draw(e.Graphics, Icon, iconBounds, TintForeground, Math.Max(1.4f, Width * 0.06f));
    }
}
