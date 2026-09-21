using System.Drawing.Drawing2D;

namespace RemoteDesktopClient.UI.WinForms.Controls;

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
/// Small, simple line-icons drawn directly with GDI+ (no image assets), so
/// every icon scales cleanly and always matches the current theme color.
/// Each icon is drawn to fill <paramref name="bounds"/>.
/// </summary>
internal static class Icons
{
    public static void Draw(Graphics g, IconKind kind, RectangleF bounds, Color color, float strokeWidth = 1.6f)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using var pen = new Pen(color, strokeWidth) { LineJoin = LineJoin.Round, StartCap = LineCap.Round, EndCap = LineCap.Round };
        using var brush = new SolidBrush(color);

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

    private static void DrawMonitor(Graphics g, RectangleF b, Pen pen)
    {
        var screen = new RectangleF(b.Left, b.Top, b.Width, b.Height * 0.66f);
        using var path = RoundedRect.CreatePath(Rectangle.Round(screen), 3);
        g.DrawPath(pen, path);
        float standTop = screen.Bottom;
        float centerX = b.Left + b.Width / 2f;
        g.DrawLine(pen, centerX, standTop, centerX, b.Bottom - 3);
        g.DrawLine(pen, b.Left + b.Width * 0.28f, b.Bottom - 1, b.Right - b.Width * 0.28f, b.Bottom - 1);
    }

    private static void DrawHome(Graphics g, RectangleF b, Pen pen)
    {
        float midX = b.Left + b.Width / 2f;
        float roofY = b.Top;
        float eaveY = b.Top + b.Height * 0.42f;
        g.DrawLine(pen, midX, roofY, b.Left, eaveY);
        g.DrawLine(pen, midX, roofY, b.Right, eaveY);
        g.DrawLine(pen, b.Left + 1, eaveY, b.Left + 1, b.Bottom);
        g.DrawLine(pen, b.Right - 1, eaveY, b.Right - 1, b.Bottom);
        g.DrawLine(pen, b.Left + 1, b.Bottom, b.Right - 1, b.Bottom);
        var door = new RectangleF(midX - b.Width * 0.14f, b.Bottom - b.Height * 0.36f, b.Width * 0.28f, b.Height * 0.36f);
        g.DrawRectangle(pen, door.X, door.Y, door.Width, door.Height);
    }

    private static void DrawLaptop(Graphics g, RectangleF b, Pen pen)
    {
        var screen = new RectangleF(b.Left + b.Width * 0.12f, b.Top, b.Width * 0.76f, b.Height * 0.62f);
        using var path = RoundedRect.CreatePath(Rectangle.Round(screen), 2);
        g.DrawPath(pen, path);
        float baseY = b.Bottom - b.Height * 0.12f;
        g.DrawLine(pen, b.Left, baseY, b.Right, baseY);
        g.DrawLine(pen, b.Left, baseY, b.Left + b.Width * 0.12f, b.Bottom);
        g.DrawLine(pen, b.Right, baseY, b.Right - b.Width * 0.12f, b.Bottom);
        g.DrawLine(pen, b.Left + b.Width * 0.12f, b.Bottom, b.Right - b.Width * 0.12f, b.Bottom);
    }

    private static void DrawServer(Graphics g, RectangleF b, Pen pen, Brush brush)
    {
        float unitHeight = b.Height * 0.42f;
        float gap = b.Height * 0.16f;
        for (int i = 0; i < 2; i++)
        {
            var unit = new RectangleF(b.Left, b.Top + i * (unitHeight + gap), b.Width, unitHeight);
            using var path = RoundedRect.CreatePath(Rectangle.Round(unit), 2);
            g.DrawPath(pen, path);
            g.FillEllipse(brush, unit.Left + unit.Width * 0.12f, unit.Top + unit.Height / 2f - 1.5f, 3, 3);
        }
    }

    private static void DrawLink(Graphics g, RectangleF b, Pen pen)
    {
        var state = g.Save();
        g.TranslateTransform(b.Left + b.Width / 2f, b.Top + b.Height / 2f);
        g.RotateTransform(-45f);
        float w = b.Width * 0.62f, h = b.Height * 0.4f;
        using var loop1 = RoundedRect.CreatePath(new Rectangle((int)(-w), (int)(-h / 2 - h * 0.55f), (int)w, (int)h), (int)(h / 2));
        using var loop2 = RoundedRect.CreatePath(new Rectangle((int)(0), (int)(-h / 2 + h * 0.55f), (int)w, (int)h), (int)(h / 2));
        g.DrawPath(pen, loop1);
        g.DrawPath(pen, loop2);
        g.Restore(state);
    }

    private static void DrawClock(Graphics g, RectangleF b, Pen pen)
    {
        g.DrawEllipse(pen, b);
        float cx = b.Left + b.Width / 2f, cy = b.Top + b.Height / 2f;
        g.DrawLine(pen, cx, cy, cx, b.Top + b.Height * 0.28f);
        g.DrawLine(pen, cx, cy, cx + b.Width * 0.22f, cy + b.Height * 0.02f);
    }

    private static void DrawClipboard(Graphics g, RectangleF b, Pen pen)
    {
        var body = new RectangleF(b.Left, b.Top + b.Height * 0.08f, b.Width, b.Height * 0.92f);
        using var path = RoundedRect.CreatePath(Rectangle.Round(body), 2);
        g.DrawPath(pen, path);
        var tab = new RectangleF(b.Left + b.Width * 0.28f, b.Top, b.Width * 0.44f, b.Height * 0.16f);
        using var tabPath = RoundedRect.CreatePath(Rectangle.Round(tab), 1);
        g.DrawPath(pen, tabPath);
        float lineY1 = body.Top + body.Height * 0.42f;
        float lineY2 = body.Top + body.Height * 0.64f;
        g.DrawLine(pen, body.Left + body.Width * 0.2f, lineY1, body.Right - body.Width * 0.2f, lineY1);
        g.DrawLine(pen, body.Left + body.Width * 0.2f, lineY2, body.Right - body.Width * 0.2f, lineY2);
    }

    private static void DrawCopy(Graphics g, RectangleF b, Pen pen)
    {
        var back = new RectangleF(b.Left + b.Width * 0.2f, b.Top, b.Width * 0.65f, b.Height * 0.65f);
        var front = new RectangleF(b.Left, b.Top + b.Height * 0.35f, b.Width * 0.65f, b.Height * 0.65f);
        using var backPath = RoundedRect.CreatePath(Rectangle.Round(back), 2);
        using var frontPath = RoundedRect.CreatePath(Rectangle.Round(front), 2);
        g.DrawPath(pen, backPath);
        g.FillPath(Brushes.White, frontPath);
        g.DrawPath(pen, frontPath);
    }

    private static void DrawSend(Graphics g, RectangleF b, Brush brush)
    {
        var points = new[]
        {
            new PointF(b.Left, b.Top + b.Height * 0.06f),
            new PointF(b.Right, b.Top + b.Height / 2f),
            new PointF(b.Left, b.Bottom - b.Height * 0.06f),
            new PointF(b.Left + b.Width * 0.32f, b.Top + b.Height / 2f),
        };
        g.FillPolygon(brush, points);
    }

    private static void DrawTrash(Graphics g, RectangleF b, Pen pen)
    {
        var body = new RectangleF(b.Left + b.Width * 0.12f, b.Top + b.Height * 0.22f, b.Width * 0.76f, b.Height * 0.72f);
        using var path = RoundedRect.CreatePath(Rectangle.Round(body), 2);
        g.DrawPath(pen, path);
        g.DrawLine(pen, b.Left, b.Top + b.Height * 0.22f, b.Right, b.Top + b.Height * 0.22f);
        g.DrawLine(pen, b.Left + b.Width * 0.36f, b.Top + b.Height * 0.22f, b.Left + b.Width * 0.36f, b.Top);
        g.DrawLine(pen, b.Right - b.Width * 0.36f, b.Top + b.Height * 0.22f, b.Right - b.Width * 0.36f, b.Top);
        g.DrawLine(pen, b.Left + b.Width * 0.36f, b.Top, b.Right - b.Width * 0.36f, b.Top);
        float x1 = body.Left + body.Width * 0.3f, x2 = body.Left + body.Width * 0.5f, x3 = body.Left + body.Width * 0.7f;
        g.DrawLine(pen, x1, body.Top + body.Height * 0.22f, x1, body.Bottom - body.Height * 0.15f);
        g.DrawLine(pen, x2, body.Top + body.Height * 0.22f, x2, body.Bottom - body.Height * 0.15f);
        g.DrawLine(pen, x3, body.Top + body.Height * 0.22f, x3, body.Bottom - body.Height * 0.15f);
    }

    private static void DrawGear(Graphics g, RectangleF b, Pen pen, Brush brush)
    {
        float cx = b.Left + b.Width / 2f, cy = b.Top + b.Height / 2f;
        float outerR = b.Width / 2f;
        float innerR = outerR * 0.55f;
        const int teeth = 8;
        for (int i = 0; i < teeth; i++)
        {
            double angle = i * (Math.PI * 2 / teeth);
            float tx = cx + (float)Math.Cos(angle) * outerR;
            float ty = cy + (float)Math.Sin(angle) * outerR;
            g.FillEllipse(brush, tx - 1.6f, ty - 1.6f, 3.2f, 3.2f);
        }
        g.DrawEllipse(pen, cx - innerR, cy - innerR, innerR * 2, innerR * 2);
        g.DrawEllipse(pen, cx - innerR * 0.35f, cy - innerR * 0.35f, innerR * 0.7f, innerR * 0.7f);
    }

    private static void DrawDots(Graphics g, RectangleF b, Brush brush)
    {
        float cx = b.Left + b.Width / 2f;
        float d = Math.Min(b.Width, b.Height * 0.22f);
        float spacing = b.Height * 0.36f;
        float top = b.Top + b.Height / 2f - spacing;
        for (int i = 0; i < 3; i++)
        {
            g.FillEllipse(brush, cx - d / 2f, top + i * spacing - d / 2f, d, d);
        }
    }

    private static void DrawCheck(Graphics g, RectangleF b, Pen pen)
    {
        g.DrawLine(pen, b.Left, b.Top + b.Height * 0.55f, b.Left + b.Width * 0.38f, b.Bottom);
        g.DrawLine(pen, b.Left + b.Width * 0.38f, b.Bottom, b.Right, b.Top);
    }

    private static void DrawPlay(Graphics g, RectangleF b, Brush brush)
    {
        var points = new[]
        {
            new PointF(b.Left + b.Width * 0.15f, b.Top),
            new PointF(b.Right - b.Width * 0.1f, b.Top + b.Height / 2f),
            new PointF(b.Left + b.Width * 0.15f, b.Bottom),
        };
        g.FillPolygon(brush, points);
    }

    private static void DrawChevron(Graphics g, RectangleF b, Pen pen)
    {
        g.DrawLine(pen, b.Left, b.Top + b.Height * 0.3f, b.Left + b.Width / 2f, b.Bottom - b.Height * 0.25f);
        g.DrawLine(pen, b.Left + b.Width / 2f, b.Bottom - b.Height * 0.25f, b.Right, b.Top + b.Height * 0.3f);
    }
}
