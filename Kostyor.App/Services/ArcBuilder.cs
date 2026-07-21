using System.Windows;
using System.Windows.Media;

namespace Kostyor.App.Services;

/// <summary>
/// Строит геометрию дуги кольца-прогресса (замена SVG <c>stroke-dashoffset</c> из дизайна).
/// Дуга начинается сверху (−90°) и идёт по часовой на <c>progress·360°</c>. Радиус/центр — как в макете.
/// </summary>
public static class ArcBuilder
{
    public static Geometry Build(double progress, double centerX = 210, double centerY = 210, double radius = 198)
    {
        progress = Math.Clamp(progress, 0d, 1d);

        if (progress <= 0d)
            return Geometry.Empty;

        if (progress >= 1d)
            return new EllipseGeometry(new Point(centerX, centerY), radius, radius);

        const double startAngle = -90d;
        var endAngle = startAngle + progress * 360d;

        var start = PointOnCircle(centerX, centerY, radius, startAngle);
        var end = PointOnCircle(centerX, centerY, radius, endAngle);

        var figure = new PathFigure { StartPoint = start, IsClosed = false, IsFilled = false };
        figure.Segments.Add(new ArcSegment
        {
            Point = end,
            Size = new Size(radius, radius),
            IsLargeArc = progress > 0.5d,
            SweepDirection = SweepDirection.Clockwise,
            RotationAngle = 0,
        });

        var geo = new PathGeometry();
        geo.Figures.Add(figure);
        geo.Freeze();
        return geo;
    }

    private static Point PointOnCircle(double cx, double cy, double r, double angleDeg)
    {
        var rad = angleDeg * Math.PI / 180d;
        return new Point(cx + r * Math.Cos(rad), cy + r * Math.Sin(rad));
    }
}
