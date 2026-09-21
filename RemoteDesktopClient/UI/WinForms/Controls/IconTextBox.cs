using System.ComponentModel;
using System.Drawing.Drawing2D;

namespace RemoteDesktopClient.UI.WinForms.Controls;

/// <summary>A bordered input box with a leading icon, wrapping a real, borderless
/// <see cref="TextBox"/> so typing, focus and placeholder text all behave natively.</summary>
internal sealed class IconTextBox : Panel
{
    // Not designer-bound (this project builds the UI entirely in code), so these
    // are hidden from the WinForms designer's property serialization.
    public TextBox InnerTextBox { get; }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public IconKind Icon { get; set; } = IconKind.Monitor;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string PlaceholderText
    {
        get => InnerTextBox.PlaceholderText;
        set => InnerTextBox.PlaceholderText = value;
    }

    // Deliberately hides Control.Text/Control.TextChanged — this control's
    // "text" is whatever the wrapped TextBox contains, not its own caption.
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public new string Text
    {
        get => InnerTextBox.Text;
        set => InnerTextBox.Text = value;
    }

    public new event EventHandler? TextChanged
    {
        add => InnerTextBox.TextChanged += value;
        remove => InnerTextBox.TextChanged -= value;
    }

    public IconTextBox()
    {
        // Constructed before Height is set below — setting Height triggers
        // OnResize -> PositionTextBox(), which reads InnerTextBox.
        InnerTextBox = new TextBox
        {
            BorderStyle = BorderStyle.None,
            Font = Theme.FontBody,
            Location = new Point(40, 15),
        };

        BackColor = Theme.CardBackground;
        Height = 48;

        InnerTextBox.Resize += (_, _) => PositionTextBox();

        Controls.Add(InnerTextBox);
    }

    // TableLayoutPanel sizes an AutoSize row/column from GetPreferredSize(),
    // not from the Size property directly (see the same override on
    // StatusBadge, IdChip and ModernButton).
    public override Size GetPreferredSize(Size proposedSize) => Size;

    private void PositionTextBox()
    {
        InnerTextBox.Location = new Point(40, (Height - InnerTextBox.Height) / 2);
        InnerTextBox.Width = Width - 40 - 12;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

        var rect = new Rectangle(0, 0, Width - 1, Height - 1);
        using var path = RoundedRect.CreatePath(rect, 10);
        using var fill = new SolidBrush(Theme.CardBackground);
        e.Graphics.FillPath(fill, path);
        using var pen = new Pen(Theme.CardBorder);
        e.Graphics.DrawPath(pen, path);

        var iconBounds = new RectangleF(14, Height / 2f - 8, 16, 16);
        Icons.Draw(e.Graphics, Icon, iconBounds, Theme.TextSecondary, 1.5f);
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        PositionTextBox();
    }
}
