using System.Drawing.Drawing2D;

namespace RemoteDesktopClient.UI.WinForms.Controls;

/// <summary>Decorative laptop-with-checkmark graphic used beside the "Ready to
/// connect" message on the This Device panel. Purely ornamental.</summary>
internal sealed class HeroIllustration : Control
{
    public HeroIllustration()
    {
        SetStyle(ControlStyles.SupportsTransparentBackColor, true);
        DoubleBuffered = true;
        BackColor = Color.Transparent;
        Size = new Size(140, 120);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

        // Soft background blobs.
        using (var blob = new SolidBrush(Color.FromArgb(60, Theme.Accent)))
        {
            e.Graphics.FillEllipse(blob, Width * 0.45f, 0, Width * 0.55f, Width * 0.55f);
        }
        using (var blob = new SolidBrush(Color.FromArgb(35, Theme.Accent)))
        {
            e.Graphics.FillEllipse(blob, 0, Height * 0.35f, Width * 0.4f, Width * 0.4f);
        }

        // Laptop.
        float laptopWidth = Width * 0.62f;
        float laptopLeft = (Width - laptopWidth) / 2f;
        var screen = new RectangleF(laptopLeft, Height * 0.12f, laptopWidth, laptopWidth * 0.62f);
        using (var pen = new Pen(Color.FromArgb(180, Theme.TextSecondary), 2f))
        {
            using var screenPath = RoundedRect.CreatePath(Rectangle.Round(screen), 6);
            e.Graphics.DrawPath(pen, screenPath);

            float baseY = screen.Bottom + 10;
            e.Graphics.DrawLine(pen, laptopLeft - 10, baseY, screen.Right + 10, baseY);
            e.Graphics.DrawLine(pen, laptopLeft - 10, baseY, laptopLeft + 6, screen.Bottom + 2);
            e.Graphics.DrawLine(pen, screen.Right + 10, baseY, screen.Right - 6, screen.Bottom + 2);
        }

        // Green "ready" checkmark badge, overlapping the screen.
        float badgeSize = Width * 0.30f;
        var badgeRect = new RectangleF(
            laptopLeft + laptopWidth / 2f - badgeSize / 2f,
            screen.Top + screen.Height / 2f - badgeSize / 2f,
            badgeSize, badgeSize);
        using (var badgeBrush = new SolidBrush(Theme.Online))
        {
            e.Graphics.FillEllipse(badgeBrush, badgeRect);
        }
        var checkBounds = new RectangleF(
            badgeRect.Left + badgeRect.Width * 0.26f,
            badgeRect.Top + badgeRect.Height * 0.30f,
            badgeRect.Width * 0.48f,
            badgeRect.Height * 0.40f);
        using (var checkPen = new Pen(Color.White, 2.4f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
        {
            e.Graphics.DrawLine(checkPen, checkBounds.Left, checkBounds.Top + checkBounds.Height * 0.55f, checkBounds.Left + checkBounds.Width * 0.38f, checkBounds.Bottom);
            e.Graphics.DrawLine(checkPen, checkBounds.Left + checkBounds.Width * 0.38f, checkBounds.Bottom, checkBounds.Right, checkBounds.Top);
        }
    }
}
