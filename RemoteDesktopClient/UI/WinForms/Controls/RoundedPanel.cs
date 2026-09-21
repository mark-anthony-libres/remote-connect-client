using System.ComponentModel;
using System.Drawing.Drawing2D;

namespace RemoteDesktopClient.UI.WinForms.Controls;

/// <summary>A card-style panel with rounded corners, used to group each section of the UI.</summary>
internal sealed class RoundedPanel : Panel
{
    // Not designer-bound (this project builds the UI entirely in code), so these
    // are hidden from the WinForms designer's property serialization.
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int CornerRadius { get; set; } = 12;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color CardColor { get; set; } = Theme.CardBackground;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color BorderColor { get; set; } = Theme.CardBorder;

    public RoundedPanel()
    {
        DoubleBuffered = true;
        ResizeRedraw = true;
        // BackColor matches the page background (not the card color) so the square
        // corners left outside the rounded path blend into the page instead of showing.
        BackColor = Theme.Background;
        Padding = new Padding(24);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

        var rect = new Rectangle(0, 0, Width - 1, Height - 1);
        using var path = RoundedRect.CreatePath(rect, CornerRadius);

        using var fill = new SolidBrush(CardColor);
        e.Graphics.FillPath(fill, path);

        using var pen = new Pen(BorderColor);
        e.Graphics.DrawPath(pen, path);
    }
}
