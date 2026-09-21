using System.ComponentModel;
using System.Drawing.Drawing2D;

namespace RemoteDesktopClient.UI.WinForms.Controls;

/// <summary>A colored pill showing whether a device is online or offline.</summary>
internal sealed class StatusBadge : Control
{
    private bool _isOnline;

    // Not designer-bound (this project builds the UI entirely in code), so this
    // is hidden from the WinForms designer's property serialization.
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool IsOnline
    {
        get => _isOnline;
        set { _isOnline = value; Invalidate(); }
    }

    public StatusBadge()
    {
        SetStyle(ControlStyles.SupportsTransparentBackColor, true);
        DoubleBuffered = true;
        Font = Theme.FontSmall;
        Size = new Size(76, 22);
        BackColor = Color.Transparent;
    }

    // TableLayoutPanel sizes an AutoSize row/column from GetPreferredSize(), not
    // from the Size property directly — without this override the row collapses.
    public override Size GetPreferredSize(Size proposedSize) => Size;

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

        var pillColor = IsOnline ? Theme.OnlineSoft : Theme.OfflineSoft;
        var dotColor = IsOnline ? Theme.Online : Theme.Offline;
        string text = IsOnline ? "Online" : "Offline";

        var rect = new Rectangle(0, 0, Width - 1, Height - 1);
        using var path = RoundedRect.CreatePath(rect, rect.Height / 2);
        using var fill = new SolidBrush(pillColor);
        e.Graphics.FillPath(fill, path);

        const int dotSize = 7;
        int dotX = 10;
        int dotY = (Height - dotSize) / 2;
        using var dotBrush = new SolidBrush(dotColor);
        e.Graphics.FillEllipse(dotBrush, dotX, dotY, dotSize, dotSize);

        var textRect = new Rectangle(dotX + dotSize + 6, 0, Width - dotX - dotSize - 12, Height);
        TextRenderer.DrawText(
            e.Graphics, text, Font, textRect, dotColor,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
    }
}
