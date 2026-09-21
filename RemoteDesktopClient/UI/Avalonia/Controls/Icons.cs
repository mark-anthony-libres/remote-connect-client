using Avalonia;
using Avalonia.Media;

namespace RemoteDesktopClient.UI.Avalonia.Controls;

internal enum IconKind
{
    Monitor,
    Home,
    Laptop,
    Server,
    Link,
    Clock,
    Clipboard,
    Copy,
    Send,
    Trash,
    Gear,
    Dots,
    Check,
    Play,
    Chevron,
}

/// <summary>
/// Small, simple line-icons drawn directly with Avalonia's DrawingContext (no
/// image assets), so every icon scales cleanly and always matches the current
/// theme color. Each icon is drawn to fill <paramref name="bounds"/>.
/// </summary>
internal static class Icons
{
    public static void Draw(DrawingContext g, IconKind kind, Rect bounds, Color color, double strokeWidth = 1.6)
    {
        var pen = new Pen(new SolidColorBrush(color), strokeWidth, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
        var brush = new SolidColorBrush(color);

        switch (kind)
        {
            case IconKind.Monitor: DrawMonitor(g, bounds, pen); break;
            case IconKind.Home: DrawHome(g, bounds, pen); break;
            case IconKind.Laptop: DrawLaptop(g, bounds, pen); break;
            case IconKind.Server: DrawServer(g, bounds, pen, brush); break;
            case IconKind.Link: DrawLink(g, bounds, pen); break;
            case IconKind.Clock: DrawClock(g, bounds, pen); break;
            case IconKind.Clipboard: DrawClipboard(g, bounds, pen); break;
            case IconKind.Copy: DrawCopy(g, bounds, pen); break;
            case IconKind.Send: DrawSend(g, bounds, brush); break;
            case IconKind.Trash: DrawTrash(g, bounds, pen); break;
            case IconKind.Gear: DrawGear(g, bounds, pen, brush); break;
            case IconKind.Dots: DrawDots(g, bounds, brush); break;
            case IconKind.Check: DrawCheck(g, bounds, pen); break;
            case IconKind.Play: DrawPlay(g, bounds, brush); break;
            case IconKind.Chevron: DrawChevron(g, bounds, pen); break;
        }
    }

    private static void DrawMonitor(DrawingContext g, Rect b, Pen pen)
    {
        var screen = new Rect(b.Left, b.Top, b.Width, b.Height * 0.66);
        g.DrawRectangle(null, pen, new RoundedRect(screen, 3));
        double standTop = screen.Bottom;
        double centerX = b.Left + b.Width / 2;
        g.DrawLine(pen, new Point(centerX, standTop), new Point(centerX, b.Bottom - 3));
        g.DrawLine(pen, new Point(b.Left + b.Width * 0.28, b.Bottom - 1), new Point(b.Right - b.Width * 0.28, b.Bottom - 1));
    }

    private static void DrawHome(DrawingContext g, Rect b, Pen pen)
    {
        double midX = b.Left + b.Width / 2;
        double roofY = b.Top;
        double eaveY = b.Top + b.Height * 0.42;
        g.DrawLine(pen, new Point(midX, roofY), new Point(b.Left, eaveY));
        g.DrawLine(pen, new Point(midX, roofY), new Point(b.Right, eaveY));
        g.DrawLine(pen, new Point(b.Left + 1, eaveY), new Point(b.Left + 1, b.Bottom));
        g.DrawLine(pen, new Point(b.Right - 1, eaveY), new Point(b.Right - 1, b.Bottom));
        g.DrawLine(pen, new Point(b.Left + 1, b.Bottom), new Point(b.Right - 1, b.Bottom));
        var door = new Rect(midX - b.Width * 0.14, b.Bottom - b.Height * 0.36, b.Width * 0.28, b.Height * 0.36);
        g.DrawRectangle(null, pen, door);
    }

    private static void DrawLaptop(DrawingContext g, Rect b, Pen pen)
    {
        var screen = new Rect(b.Left + b.Width * 0.12, b.Top, b.Width * 0.76, b.Height * 0.62);
        g.DrawRectangle(null, pen, new RoundedRect(screen, 2));
        double baseY = b.Bottom - b.Height * 0.12;
        g.DrawLine(pen, new Point(b.Left, baseY), new Point(b.Right, baseY));
        g.DrawLine(pen, new Point(b.Left, baseY), new Point(b.Left + b.Width * 0.12, b.Bottom));
        g.DrawLine(pen, new Point(b.Right, baseY), new Point(b.Right - b.Width * 0.12, b.Bottom));
        g.DrawLine(pen, new Point(b.Left + b.Width * 0.12, b.Bottom), new Point(b.Right - b.Width * 0.12, b.Bottom));
    }

    private static void DrawServer(DrawingContext g, Rect b, Pen pen, IBrush brush)
    {
        double unitHeight = b.Height * 0.42;
        double gap = b.Height * 0.16;
        for (int i = 0; i < 2; i++)
        {
            var unit = new Rect(b.Left, b.Top + i * (unitHeight + gap), b.Width, unitHeight);
            g.DrawRectangle(null, pen, new RoundedRect(unit, 2));
            g.DrawEllipse(brush, null, new Point(unit.Left + unit.Width * 0.12 + 1.5, unit.Top + unit.Height / 2), 1.5, 1.5);
        }
    }

    private static void DrawLink(DrawingContext g, Rect b, Pen pen)
    {
        using (g.PushTransform(Matrix.CreateTranslation(b.Left + b.Width / 2, b.Top + b.Height / 2) * Matrix.CreateRotation(-45 * Math.PI / 180)))
        {
            double w = b.Width * 0.62, h = b.Height * 0.4;
            var loop1 = new Rect(-w, -h / 2 - h * 0.55, w, h);
            var loop2 = new Rect(0, -h / 2 + h * 0.55, w, h);
            g.DrawRectangle(null, pen, new RoundedRect(loop1, h / 2));
            g.DrawRectangle(null, pen, new RoundedRect(loop2, h / 2));
        }
    }

    private static void DrawClock(DrawingContext g, Rect b, Pen pen)
    {
        g.DrawEllipse(null, pen, b.Center, b.Width / 2, b.Height / 2);
        double cx = b.Left + b.Width / 2, cy = b.Top + b.Height / 2;
        g.DrawLine(pen, new Point(cx, cy), new Point(cx, b.Top + b.Height * 0.28));
        g.DrawLine(pen, new Point(cx, cy), new Point(cx + b.Width * 0.22, cy + b.Height * 0.02));
    }

    private static void DrawClipboard(DrawingContext g, Rect b, Pen pen)
    {
        var body = new Rect(b.Left, b.Top + b.Height * 0.08, b.Width, b.Height * 0.92);
        g.DrawRectangle(null, pen, new RoundedRect(body, 2));
        var tab = new Rect(b.Left + b.Width * 0.28, b.Top, b.Width * 0.44, b.Height * 0.16);
        g.DrawRectangle(null, pen, new RoundedRect(tab, 1));
        double lineY1 = body.Top + body.Height * 0.42;
        double lineY2 = body.Top + body.Height * 0.64;
        g.DrawLine(pen, new Point(body.Left + body.Width * 0.2, lineY1), new Point(body.Right - body.Width * 0.2, lineY1));
        g.DrawLine(pen, new Point(body.Left + body.Width * 0.2, lineY2), new Point(body.Right - body.Width * 0.2, lineY2));
    }

    private static void DrawCopy(DrawingContext g, Rect b, Pen pen)
    {
        var back = new Rect(b.Left + b.Width * 0.2, b.Top, b.Width * 0.65, b.Height * 0.65);
        var front = new Rect(b.Left, b.Top + b.Height * 0.35, b.Width * 0.65, b.Height * 0.65);
        g.DrawRectangle(null, pen, new RoundedRect(back, 2));
        g.DrawRectangle(Brushes.White, pen, new RoundedRect(front, 2));
    }

    private static void DrawSend(DrawingContext g, Rect b, IBrush brush)
    {
        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(new Point(b.Left, b.Top + b.Height * 0.06), isFilled: true);
            ctx.LineTo(new Point(b.Right, b.Top + b.Height / 2));
            ctx.LineTo(new Point(b.Left, b.Bottom - b.Height * 0.06));
            ctx.LineTo(new Point(b.Left + b.Width * 0.32, b.Top + b.Height / 2));
            ctx.EndFigure(true);
        }
        g.DrawGeometry(brush, null, geometry);
    }

    private static void DrawTrash(DrawingContext g, Rect b, Pen pen)
    {
        var body = new Rect(b.Left + b.Width * 0.12, b.Top + b.Height * 0.22, b.Width * 0.76, b.Height * 0.72);
        g.DrawRectangle(null, pen, new RoundedRect(body, 2));
        g.DrawLine(pen, new Point(b.Left, b.Top + b.Height * 0.22), new Point(b.Right, b.Top + b.Height * 0.22));
        g.DrawLine(pen, new Point(b.Left + b.Width * 0.36, b.Top + b.Height * 0.22), new Point(b.Left + b.Width * 0.36, b.Top));
        g.DrawLine(pen, new Point(b.Right - b.Width * 0.36, b.Top + b.Height * 0.22), new Point(b.Right - b.Width * 0.36, b.Top));
        g.DrawLine(pen, new Point(b.Left + b.Width * 0.36, b.Top), new Point(b.Right - b.Width * 0.36, b.Top));
        double x1 = body.Left + body.Width * 0.3, x2 = body.Left + body.Width * 0.5, x3 = body.Left + body.Width * 0.7;
        g.DrawLine(pen, new Point(x1, body.Top + body.Height * 0.22), new Point(x1, body.Bottom - body.Height * 0.15));
        g.DrawLine(pen, new Point(x2, body.Top + body.Height * 0.22), new Point(x2, body.Bottom - body.Height * 0.15));
        g.DrawLine(pen, new Point(x3, body.Top + body.Height * 0.22), new Point(x3, body.Bottom - body.Height * 0.15));
    }

    private static void DrawGear(DrawingContext g, Rect b, Pen pen, IBrush brush)
    {
        double cx = b.Left + b.Width / 2, cy = b.Top + b.Height / 2;
        double outerR = b.Width / 2;
        double innerR = outerR * 0.55;
        const int teeth = 8;
        for (int i = 0; i < teeth; i++)
        {
            double angle = i * (Math.PI * 2 / teeth);
            double tx = cx + Math.Cos(angle) * outerR;
            double ty = cy + Math.Sin(angle) * outerR;
            g.DrawEllipse(brush, null, new Point(tx, ty), 1.6, 1.6);
        }
        g.DrawEllipse(null, pen, new Point(cx, cy), innerR, innerR);
        g.DrawEllipse(null, pen, new Point(cx, cy), innerR * 0.35, innerR * 0.35);
    }

    private static void DrawDots(DrawingContext g, Rect b, IBrush brush)
    {
        double cx = b.Left + b.Width / 2;
        double d = Math.Min(b.Width, b.Height * 0.22);
        double spacing = b.Height * 0.36;
        double top = b.Top + b.Height / 2 - spacing;
        for (int i = 0; i < 3; i++)
        {
            g.DrawEllipse(brush, null, new Point(cx, top + i * spacing), d / 2, d / 2);
        }
    }

    private static void DrawCheck(DrawingContext g, Rect b, Pen pen)
    {
        g.DrawLine(pen, new Point(b.Left, b.Top + b.Height * 0.55), new Point(b.Left + b.Width * 0.38, b.Bottom));
        g.DrawLine(pen, new Point(b.Left + b.Width * 0.38, b.Bottom), new Point(b.Right, b.Top));
    }

    private static void DrawPlay(DrawingContext g, Rect b, IBrush brush)
    {
        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(new Point(b.Left + b.Width * 0.15, b.Top), isFilled: true);
            ctx.LineTo(new Point(b.Right - b.Width * 0.1, b.Top + b.Height / 2));
            ctx.LineTo(new Point(b.Left + b.Width * 0.15, b.Bottom));
            ctx.EndFigure(true);
        }
        g.DrawGeometry(brush, null, geometry);
    }

    private static void DrawChevron(DrawingContext g, Rect b, Pen pen)
    {
        g.DrawLine(pen, new Point(b.Left, b.Top + b.Height * 0.3), new Point(b.Left + b.Width / 2, b.Bottom - b.Height * 0.25));
        g.DrawLine(pen, new Point(b.Left + b.Width / 2, b.Bottom - b.Height * 0.25), new Point(b.Right, b.Top + b.Height * 0.3));
    }
}
