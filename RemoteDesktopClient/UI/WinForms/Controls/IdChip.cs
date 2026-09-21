using System.ComponentModel;
using System.Drawing.Drawing2D;

namespace RemoteDesktopClient.UI.WinForms.Controls;

/// <summary>A white, bordered pill showing a device ID with a clipboard icon
/// on the left and a copy icon on the right. Clicking anywhere on it copies
/// the ID to the clipboard.</summary>
internal sealed class IdChip : Control
{
    private string _deviceId = string.Empty;
    private bool _isHovered;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string DeviceId
    {
        get => _deviceId;
        set
        {
            _deviceId = value;
            AutoSizeToContent();
            Invalidate();
        }
    }

    public event EventHandler? CopyRequested;

    public IdChip()
    {
        DoubleBuffered = true;
        Font = Theme.FontMonoId;
        Height = 44;
        Cursor = Cursors.Hand;

        MouseEnter += (_, _) => { _isHovered = true; Invalidate(); };
        MouseLeave += (_, _) => { _isHovered = false; Invalidate(); };
        Click += (_, _) =>
        {
            if (!string.IsNullOrEmpty(_deviceId))
            {
                Clipboard.SetText(_deviceId);
            }
            CopyRequested?.Invoke(this, EventArgs.Empty);
        };
    }

    private void AutoSizeToContent()
    {
        var textSize = TextRenderer.MeasureText(_deviceId, Font);
        Width = textSize.Width + 96; // room for the clipboard icon, padding and copy icon
    }

    public override Size GetPreferredSize(Size proposedSize) => Size;

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

        var rect = new Rectangle(0, 0, Width - 1, Height - 1);
        using var path = RoundedRect.CreatePath(rect, 10);
        using var fill = new SolidBrush(_isHovered ? Theme.SubtleButtonBackground : Theme.CardBackground);
        e.Graphics.FillPath(fill, path);
        using var pen = new Pen(Theme.CardBorder);
        e.Graphics.DrawPath(pen, path);

        var clipboardBounds = new RectangleF(14, Height / 2f - 9, 18, 18);
        Icons.Draw(e.Graphics, IconKind.Clipboard, clipboardBounds, Theme.TextSecondary, 1.4f);

        var textRect = new Rectangle(40, 0, Width - 40 - 36, Height);
        TextRenderer.DrawText(
            e.Graphics, _deviceId, Font, textRect, Theme.Accent,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter);

        var copyBounds = new RectangleF(Width - 30, Height / 2f - 8, 16, 16);
        Icons.Draw(e.Graphics, IconKind.Copy, copyBounds, Theme.TextSecondary, 1.4f);
    }
}
