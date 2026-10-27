using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace ContentWatch.Desktop;

/// <summary>Application-only decoration; never part of either OBS output.</summary>
public sealed class ProgressRing : Control
{
    public double Progress { get; set; }
    public override void Render(DrawingContext context)
    {
        var radius = Math.Max(0, Math.Min(Bounds.Width, Bounds.Height) / 2 - 5);
        var centre = new Point(Bounds.Width / 2, Bounds.Height / 2);
        context.DrawEllipse(null, new Pen(Brush.Parse("#3A4051"), 5), centre, radius, radius);
        var progress = Math.Clamp(Progress, 0, 1);
        if (progress <= 0) return;
        var pen = new Pen(Brush.Parse("#AEC7FC"), 5, lineCap: PenLineCap.Round);
        if (progress >= .9999)
        {
            context.DrawEllipse(null, pen, centre, radius, radius);
            return;
        }
        var angle = progress * Math.PI * 2 - Math.PI / 2;
        var geometry = new StreamGeometry();
        using (var path = geometry.Open())
        {
            path.BeginFigure(new Point(centre.X, centre.Y - radius), false);
            path.ArcTo(new Point(centre.X + Math.Cos(angle) * radius, centre.Y + Math.Sin(angle) * radius),
                new Size(radius, radius), 0, progress > .5, SweepDirection.Clockwise);
            path.EndFigure(false);
        }
        context.DrawGeometry(null, pen, geometry);
    }
}
