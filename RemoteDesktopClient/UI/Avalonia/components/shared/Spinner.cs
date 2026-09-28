using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;

namespace RemoteDesktopClient.UI.Avalonia.Components.Shared;

internal sealed class Spinner : Control
{
    private static readonly TimeSpan TickInterval = TimeSpan.FromMilliseconds(16);
    private const double DegreesPerTick = 6;
    private const double ArcSweepDegrees = 270;

    private readonly DispatcherTimer _timer;
    private double _angleDegrees;

    public Color Color { get; set; } = Colors.White;

    public Spinner()
    {
        _timer = new DispatcherTimer { Interval = TickInterval };
        _timer.Tick += (_, _) =>
        {
            _angleDegrees = (_angleDegrees + DegreesPerTick) % 360;
            InvalidateVisual();
        };

        AttachedToVisualTree += (_, _) => _timer.Start();
        DetachedFromVisualTree += (_, _) => _timer.Stop();
    }

    public override void Render(DrawingContext context)
    {
        var center = new Point(Bounds.Width / 2, Bounds.Height / 2);
        var radius = Math.Min(Bounds.Width, Bounds.Height) / 2 - 3;
        if (radius <= 0)
            return;

        var startPoint = PointOnCircle(center, radius, _angleDegrees);
        var endPoint = PointOnCircle(center, radius, _angleDegrees + ArcSweepDegrees);

        var geometry = new StreamGeometry();
        using (var geometryContext = geometry.Open())
        {
            geometryContext.BeginFigure(startPoint, isFilled: false);
            geometryContext.ArcTo(endPoint, new Size(radius, radius), rotationAngle: 0,
                isLargeArc: ArcSweepDegrees > 180, SweepDirection.Clockwise, isStroked: true);
        }

        var pen = new Pen(new SolidColorBrush(Color), 3, lineCap: PenLineCap.Round);
        context.DrawGeometry(null, pen, geometry);
    }

    private static Point PointOnCircle(Point center, double radius, double angleDegrees)
    {
        double angleRadians = angleDegrees * Math.PI / 180;
        return new Point(center.X + radius * Math.Cos(angleRadians), center.Y + radius * Math.Sin(angleRadians));
    }
}
