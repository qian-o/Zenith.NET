using SkiaSharp;

namespace InkCanvas;

internal sealed class Stroke(SKColor color, float width) : IDisposable
{
    private const float MinimumPointDistance = 1.5f;

    private readonly List<SKPoint> points = [];

    private SKPath? path;
    private SKRect bounds;

    private Stroke(SKColor color, float width, ReadOnlySpan<SKPoint> source) : this(color, width)
    {
        points.AddRange(source);
        bounds = ComputeBounds(source, width);
    }

    public SKColor Color { get; } = color;

    public float Width { get; } = width;

    public int PointCount => points.Count;

    public SKPath Path => path ??= BuildPath();

    public void Add(SKPoint point)
    {
        if (points.Count > 0)
        {
            SKPoint last = points[^1];
            float dx = point.X - last.X;
            float dy = point.Y - last.Y;

            if ((dx * dx) + (dy * dy) < MinimumPointDistance * MinimumPointDistance)
            {
                return;
            }
        }

        points.Add(point);

        float radius = Width * 0.5f;
        SKRect extent = new(point.X - radius, point.Y - radius, point.X + radius, point.Y + radius);
        bounds = points.Count is 1 ? extent : SKRect.Union(bounds, extent);

        path?.Dispose();
        path = null;
    }

    public List<Stroke>? Split(SKPoint from, SKPoint to, float radius)
    {
        float threshold = radius + (Width * 0.5f);
        SKRect swept = new(
            MathF.Min(from.X, to.X) - threshold,
            MathF.Min(from.Y, to.Y) - threshold,
            MathF.Max(from.X, to.X) + threshold,
            MathF.Max(from.Y, to.Y) + threshold);

        if (!swept.IntersectsWith(bounds))
        {
            return null;
        }

        float thresholdSquared = threshold * threshold;
        List<Stroke> fragments = [];
        List<SKPoint> survivors = [];
        bool touched = false;

        foreach (SKPoint point in points)
        {
            if (SegmentDistanceSquared(from, to, point) <= thresholdSquared)
            {
                touched = true;

                if (survivors.Count > 1)
                {
                    fragments.Add(new(Color, Width, [.. survivors]));
                }

                survivors.Clear();
            }
            else
            {
                survivors.Add(point);
            }
        }

        if (!touched)
        {
            return null;
        }

        if (survivors.Count > 1)
        {
            fragments.Add(new(Color, Width, [.. survivors]));
        }

        return fragments;
    }

    public void Dispose()
    {
        path?.Dispose();
    }

    private SKPath BuildPath()
    {
        using SKPathBuilder builder = new();

        if (points.Count is 1)
        {
            builder.AddCircle(points[0].X, points[0].Y, Width * 0.25f, SKPathDirection.Clockwise);

            return builder.Detach();
        }

        builder.MoveTo(points[0]);

        for (int index = 1; index < points.Count - 1; index++)
        {
            SKPoint current = points[index];
            SKPoint next = points[index + 1];
            SKPoint middle = new((current.X + next.X) * 0.5f, (current.Y + next.Y) * 0.5f);

            builder.QuadTo(current, middle);
        }

        builder.LineTo(points[^1]);

        return builder.Detach();
    }

    private static SKRect ComputeBounds(ReadOnlySpan<SKPoint> points, float width)
    {
        float radius = width * 0.5f;
        SKRect result = new(points[0].X - radius, points[0].Y - radius, points[0].X + radius, points[0].Y + radius);

        for (int index = 1; index < points.Length; index++)
        {
            SKPoint point = points[index];
            result = SKRect.Union(result, new(point.X - radius, point.Y - radius, point.X + radius, point.Y + radius));
        }

        return result;
    }

    private static float DistanceSquared(SKPoint first, SKPoint second)
    {
        float dx = first.X - second.X;
        float dy = first.Y - second.Y;

        return (dx * dx) + (dy * dy);
    }

    private static float SegmentDistanceSquared(SKPoint start, SKPoint end, SKPoint point)
    {
        float dx = end.X - start.X;
        float dy = end.Y - start.Y;
        float lengthSquared = (dx * dx) + (dy * dy);

        if (lengthSquared < float.Epsilon)
        {
            return DistanceSquared(start, point);
        }

        float amount = Math.Clamp((((point.X - start.X) * dx) + ((point.Y - start.Y) * dy)) / lengthSquared, 0.0f, 1.0f);
        SKPoint projection = new(start.X + (amount * dx), start.Y + (amount * dy));

        return DistanceSquared(projection, point);
    }
}