using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace RemoteDesktopClient.UI.Avalonia.Controls;

/// <summary>Decorative laptop-with-checkmark graphic used beside the "Ready to
/// connect" message on the This Device panel. Purely ornamental.</summary>
internal sealed class HeroIllustration : Control
{
    public HeroIllustration()
    {
        Width = 140;
        Height = 120;
    }

    public override void Render(DrawingContext context)
    {
        double width = Bounds.Width, height = Bounds.Height;

        // Soft background blobs.
        context.DrawEllipse(new SolidColorBrush(AppTheme.Accent, 60 / 255.0), null,
            new Rect(width * 0.45, 0, width * 0.55, width * 0.55));
        context.DrawEllipse(new SolidColorBrush(AppTheme.Accent, 35 / 255.0), null,
            new Rect(0, height * 0.35, width * 0.4, width * 0.4));

        // Laptop.
        double laptopWidth = width * 0.62;
        double laptopLeft = (width - laptopWidth) / 2;
        var screen = new Rect(laptopLeft, height * 0.12, laptopWidth, laptopWidth * 0.62);
        var laptopPen = new Pen(new SolidColorBrush(AppTheme.TextSecondary, 180 / 255.0), 2);
        context.DrawRectangle(null, laptopPen, new RoundedRect(screen, 6));

        double baseY = screen.Bottom + 10;
        context.DrawLine(laptopPen, new Point(laptopLeft - 10, baseY), new Point(screen.Right + 10, baseY));
        context.DrawLine(laptopPen, new Point(laptopLeft - 10, baseY), new Point(laptopLeft + 6, screen.Bottom + 2));
        context.DrawLine(laptopPen, new Point(screen.Right + 10, baseY), new Point(screen.Right - 6, screen.Bottom + 2));

        // Green "ready" checkmark badge, overlapping the screen.
        double badgeSize = width * 0.30;
        var badgeCenter = new Point(laptopLeft + laptopWidth / 2, screen.Top + screen.Height / 2);
        context.DrawEllipse(new SolidColorBrush(AppTheme.Online), null, badgeCenter, badgeSize / 2, badgeSize / 2);

        var badgeRect = new Rect(badgeCenter.X - badgeSize / 2, badgeCenter.Y - badgeSize / 2, badgeSize, badgeSize);
        var checkBounds = new Rect(
            badgeRect.Left + badgeRect.Width * 0.26,
            badgeRect.Top + badgeRect.Height * 0.30,
            badgeRect.Width * 0.48,
            badgeRect.Height * 0.40);
        var checkPen = new Pen(Brushes.White, 2.4, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
        context.DrawLine(checkPen,
            new Point(checkBounds.Left, checkBounds.Top + checkBounds.Height * 0.55),
            new Point(checkBounds.Left + checkBounds.Width * 0.38, checkBounds.Bottom));
        context.DrawLine(checkPen,
            new Point(checkBounds.Left + checkBounds.Width * 0.38, checkBounds.Bottom),
            new Point(checkBounds.Right, checkBounds.Top));
    }
}
