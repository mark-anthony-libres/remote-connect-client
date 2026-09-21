using System.ComponentModel;
using System.Drawing.Drawing2D;

namespace RemoteDesktopClient.UI.WinForms.Controls;

internal enum ButtonVariant
{
    /// <summary>Filled, accent-colored — for the main action in a section.</summary>
    Primary,

    /// <summary>Light neutral background — for quiet actions like "Settings" or "Copy ID".</summary>
    Subtle,

    /// <summary>Light accent-tinted background — for repeated row actions like "Connect" in a list.</summary>
    SoftAccent
}

/// <summary>A flat, rounded button with hover/press feedback, styled per <see cref="ButtonVariant"/>.</summary>
internal sealed class ModernButton : Button
{
    private bool _isHovered;
    private bool _isPressed;

    // Not designer-bound (this project builds the UI entirely in code), so these
    // are hidden from the WinForms designer's property serialization.
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public ButtonVariant Variant { get; set; } = ButtonVariant.Primary;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int CornerRadius { get; set; } = 8;

    /// <summary>Optional icon drawn to the left of the text. Null draws no icon.</summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public IconKind? Icon { get; set; }

    public ModernButton()
    {
        // Routes ALL painting (including background erase) through our own
        // OnPaint below. Without AllPaintingInWmPaint, Windows can still run a
        // separate native WM_ERASEBKGND pass — e.g. the native focus-rect/
        // default-border chrome a real Button draws outside owner-draw — before
        // our OnPaint runs, and if only part of the control gets invalidated
        // (a focus change, a resize) that native pass can leave stray pixels
        // our redraw never covers. This is what showed up as an intermittent
        // blue/black border.
        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.UserPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw,
            true);
        DoubleBuffered = true;
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        // Without this, Button ignores our BackColor and lets the OS visual-style
        // renderer paint its own native background underneath our custom OnPaint —
        // that native background is what shows through as dark edges/corners,
        // since OnPaint below only fills the rounded pill shape, not the full
        // rectangular control bounds.
        UseVisualStyleBackColor = false;
        Cursor = Cursors.Hand;
        Font = Theme.FontBodyBold;
        Height = 36;

        MouseEnter += (_, _) => { _isHovered = true; Invalidate(); };
        MouseLeave += (_, _) => { _isHovered = false; _isPressed = false; Invalidate(); };
        MouseDown += (_, _) => { _isPressed = true; Invalidate(); };
        MouseUp += (_, _) => { _isPressed = false; Invalidate(); };
        EnabledChanged += (_, _) => Invalidate();
        // No visible change on focus, but the native control still needs to be
        // told to repaint on these transitions (see AllPaintingInWmPaint above)
        // or a stale native frame can linger until some unrelated repaint clears it.
        GotFocus += (_, _) => Invalidate();
        LostFocus += (_, _) => Invalidate();
    }

    // Suppresses the default focus-cue visual (the mechanism WinForms/Windows
    // uses to indicate keyboard focus on a control) — this button shows focus
    // via its own hover/press coloring only, never a border or outline.
    protected override bool ShowFocusCues => false;

    // Keeps the corner triangles outside the rounded fill (see OnPaint) blended
    // with whatever the button actually sits on, instead of a hardcoded guess.
    protected override void OnParentChanged(EventArgs e)
    {
        base.OnParentChanged(e);
        if (Parent is not null)
            BackColor = Parent.BackColor;
    }

    // Ensures a fixed-size button correctly reports its size inside an
    // AutoSize TableLayoutPanel row/column (see the same override on IdChip).
    public override Size GetPreferredSize(Size proposedSize) => Size;

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

        var (background, textColor) = GetColors();

        // Unlike RoundedPanel, this shape is filled only (no border stroke), so
        // the fill must cover the full control bounds, not the Width-1/Height-1
        // rect a stroke would need to avoid clipping its pen — otherwise a 1px
        // band at the right/bottom edge is left unpainted, showing BackColor
        // through instead of the button's own fill color.
        var rect = new Rectangle(0, 0, Width, Height);
        using var path = RoundedRect.CreatePath(rect, CornerRadius);
        using var brush = new SolidBrush(background);
        e.Graphics.FillPath(brush, path);

        if (Icon is { } icon)
        {
            var textSize = TextRenderer.MeasureText(e.Graphics, Text, Font, rect.Size, TextFormatFlags.NoPadding);
            const int iconSize = 15;
            const int gap = 8;
            int groupWidth = iconSize + gap + textSize.Width;
            int startX = rect.Left + (rect.Width - groupWidth) / 2;

            var iconBounds = new RectangleF(startX, (Height - iconSize) / 2f, iconSize, iconSize);
            Icons.Draw(e.Graphics, icon, iconBounds, textColor, 1.6f);

            var textRect = new Rectangle(startX + iconSize + gap, 0, textSize.Width + 4, Height);
            TextRenderer.DrawText(
                e.Graphics, Text, Font, textRect, textColor,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
        }
        else
        {
            TextRenderer.DrawText(
                e.Graphics, Text, Font, rect, textColor,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }
    }

    private (Color Background, Color Text) GetColors()
    {
        if (!Enabled)
            return (Theme.SubtleButtonBackground, Theme.TextSecondary);

        return Variant switch
        {
            ButtonVariant.Primary => _isPressed
                ? (Theme.AccentPressed, Color.White)
                : _isHovered
                    ? (Theme.AccentHover, Color.White)
                    : (Theme.Accent, Color.White),
            ButtonVariant.Subtle => _isPressed || _isHovered
                ? (Theme.SubtleButtonHover, Theme.TextPrimary)
                : (Theme.SubtleButtonBackground, Theme.TextPrimary),
            ButtonVariant.SoftAccent => _isPressed || _isHovered
                ? (Theme.AccentSoftHover, Theme.AccentSoftText)
                : (Theme.AccentSoft, Theme.AccentSoftText),
            _ => (Theme.Accent, Color.White),
        };
    }
}
