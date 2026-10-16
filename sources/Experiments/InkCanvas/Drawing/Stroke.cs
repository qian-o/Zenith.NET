using SkiaSharp;
using Zenith.NET;

namespace InkCanvas.Drawing;

internal class Stroke(SKColor color, float width) : DisposableObject
{
    private const float MinimumPointDistance = 1.5f;

    private SKPathBuilder? builder = new();
    private SKPath? path;
    private SKRect bounds;
    private SKPoint tailStart;
    private SKPoint tailEnd;
    private int sampleCount;

    public SKColor Color { get; } = color;

    public float Width { get; } = width;

    public int NodeCount => Path.PointCount;

    public bool IsEmpty => Path.IsEmpty;

    public SKPath Path => path ??= builder!.Snapshot();

    public SKPoint TailStart => tailStart;

    public SKPoint TailEnd => tailEnd;

    public void Add(SKPoint point)
    {
        if (sampleCount > 0 && DistanceSquared(tailEnd, point) < (MinimumPointDistance * MinimumPointDistance))
        {
            return;
        }

        path?.Dispose();
        path = null;

        if (sampleCount is 0)
        {
            builder!.MoveTo(point);
            tailStart = point;
        }
        else if (sampleCount > 1)
        {
            tailStart = new((tailEnd.X + point.X) * 0.5f, (tailEnd.Y + point.Y) * 0.5f);
            builder!.QuadTo(tailEnd, tailStart);
        }

        tailEnd = point;
        sampleCount++;
    }

    public void Complete(SKPaint paint)
    {
        builder!.LineTo(tailEnd);

        path?.Dispose();
        path = null;

        using SKPath centerline = builder.Detach();

        builder.Dispose();
        builder = null;

        paint.StrokeWidth = Width;
        path = paint.GetFillPath(centerline)!;

        bounds = path.TightBounds;
    }

    public bool Erase(SKPath eraser, SKRect eraserBounds)
    {
        if (!bounds.IntersectsWith(eraserBounds))
        {
            return false;
        }

        SKPath source = Path;

        using SKPath? overlap = source.Op(eraser, SKPathOp.Intersect);

        if (overlap?.IsEmpty is not false || source.Op(eraser, SKPathOp.Difference) is not SKPath result)
        {
            return false;
        }

        source.Dispose();
        path = result;
        bounds = result.TightBounds;

        return true;
    }

    protected override void Destroy()
    {
        path?.Dispose();
        builder?.Dispose();
    }

    private static float DistanceSquared(SKPoint first, SKPoint second)
    {
        float dx = first.X - second.X;
        float dy = first.Y - second.Y;

        return (dx * dx) + (dy * dy);
    }
}
