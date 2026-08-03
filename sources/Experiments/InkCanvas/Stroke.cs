using SkiaSharp;

namespace InkCanvas;

internal sealed class Stroke(SKColor color, float width) : IDisposable
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
        if (sampleCount is 0 || DistanceSquared(tailEnd, point) >= MinimumPointDistance * MinimumPointDistance)
        {
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
    }

    public void Complete(SKPaint paint)
    {
        builder!.LineTo(tailEnd);

        path?.Dispose();

        using SKPath centerline = builder.Detach();

        builder.Dispose();
        builder = null;

        paint.StrokeWidth = Width;
        path = paint.GetFillPath(centerline)!;

        bounds = path.TightBounds;
    }

    public bool Erase(SKPath eraser, SKRect eraserBounds)
    {
        if (bounds.IntersectsWith(eraserBounds))
        {
            SKPath source = Path;

            using SKPath? overlap = source.Op(eraser, SKPathOp.Intersect);

            if (overlap?.IsEmpty is false && source.Op(eraser, SKPathOp.Difference) is { } result)
            {
                source.Dispose();
                path = result;
                bounds = result.TightBounds;

                return true;
            }
        }

        return false;
    }

    public void Dispose()
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