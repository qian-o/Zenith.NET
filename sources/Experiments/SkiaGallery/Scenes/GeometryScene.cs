using SkiaSharp;

namespace SkiaGallery;

internal sealed class GeometryScene : GalleryScene
{
    private static readonly SKColor[] DotColors =
    [
        new(106, 190, 226, 210),
        new(242, 130, 117, 210),
        new(101, 211, 163, 210),
        new(242, 190, 92, 210)
    ];

    private readonly SKPath starPath;
    private readonly SKPoint[] wavePoints = new SKPoint[96];
    private readonly SKShader shapeShader;
    private readonly SKPathEffect dashEffect;
    private readonly SKPaint shapePaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private readonly SKPaint outlinePaint = new() { Color = new(246, 250, 248, 225), IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 2.0f };
    private readonly SKPaint ghostPaint = new() { Color = new(157, 218, 222, 100), IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1.25f };
    private readonly SKPaint dashPaint = new() { Color = new(248, 199, 101, 220), IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 2.5f, StrokeCap = SKStrokeCap.Round };
    private readonly SKPaint dotPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private readonly SKPaint markerPaint = new() { Color = new(255, 247, 225), IsAntialias = true, Style = SKPaintStyle.Fill };
    private readonly SKPaint markerRingPaint = new() { Color = new(242, 130, 117), IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 3.0f };

    private SKPath? clipPath;
    private SKRect stage;
    private SKRect clipRect;
    private SKRect waveRect;
    private SKPoint starCenter;
    private SKPoint bezierStart;
    private SKPoint firstControl;
    private SKPoint secondControl;
    private SKPoint bezierEnd;
    private float starScale;

    public GeometryScene(GalleryResources resources) : base(resources)
    {
        starPath = CreateStar(72.0f, 31.0f, 7);
        shapeShader = SKShader.CreateLinearGradient(new(-78.0f, -72.0f), new(82.0f, 76.0f), [new(242, 130, 117), new(242, 190, 92), new(101, 211, 163)], [0.0f, 0.5f, 1.0f], SKShaderTileMode.Clamp);
        shapePaint.Shader = shapeShader;

        dashEffect = SKPathEffect.CreateDash([10.0f, 8.0f], 0.0f);
        dashPaint.PathEffect = dashEffect;
    }

    public override string Navigation => "Geometry";

    public override string Title => "Geometry blueprint";

    public override string Description => "Transforms, curves, clipping, and stroke phase on one construction surface.";

    public override bool IsAnimated => true;

    protected override void UpdateLayout(float width, float height)
    {
        stage = new(0.0f, 0.0f, width, height);

        if (UseWideLayout(width, height))
        {
            starCenter = new(width * 0.31f, height * 0.43f);
            starScale = Math.Clamp(MathF.Min(width * 0.17f, height * 0.25f) / 72.0f, 0.8f, 1.7f);
            clipRect = new(width * 0.66f, height * 0.18f, width * 0.93f, height * 0.61f);
            bezierStart = new(width * 0.08f, height * 0.81f);
            firstControl = new(width * 0.33f, height * 0.57f);
            secondControl = new(width * 0.64f, height * 0.94f);
            bezierEnd = new(width * 0.92f, height * 0.72f);
            waveRect = new(width * 0.09f, height * 0.90f, width * 0.91f, height * 0.97f);
        }
        else
        {
            starCenter = new(width * 0.50f, height * 0.24f);
            starScale = Math.Clamp(MathF.Min(width * 0.24f, height * 0.13f) / 72.0f, 0.72f, 1.3f);
            clipRect = new(width * 0.14f, height * 0.48f, width * 0.86f, height * 0.71f);
            bezierStart = new(width * 0.08f, height * 0.86f);
            firstControl = new(width * 0.27f, height * 0.69f);
            secondControl = new(width * 0.70f, height * 0.96f);
            bezierEnd = new(width * 0.92f, height * 0.78f);
            waveRect = new(width * 0.10f, height * 0.39f, width * 0.90f, height * 0.44f);
        }

        clipPath?.Dispose();
        using SKPathBuilder builder = new();
        builder.AddRoundRect(clipRect, 18.0f, 18.0f, SKPathDirection.Clockwise);
        clipPath = builder.Detach();
    }

    protected override void DrawDynamic(SKCanvas canvas, float width, float height, double seconds)
    {
        float time = (float)seconds;

        DrawTransformedStar(canvas, time, starScale, 16.0f, shapePaint, outlinePaint);
        DrawTransformedStar(canvas, time, starScale * 0.72f, -24.0f, null, outlinePaint);
        DrawTransformedStar(canvas, time, starScale * 0.45f, 36.0f, null, ghostPaint);

        for (int i = 0; i < wavePoints.Length; i++)
        {
            float amount = i / (wavePoints.Length - 1.0f);
            float envelope = MathF.Sin(amount * MathF.PI);
            float y = waveRect.MidY + (MathF.Sin((amount * 13.0f) + (time * 1.5f)) * waveRect.Height * 0.42f * envelope);
            wavePoints[i] = new(waveRect.Left + (amount * waveRect.Width), y);
        }

        canvas.DrawPoints(SKPointMode.Polygon, wavePoints, dashPaint);
        DrawClipField(canvas, time);

        float markerAmount = 0.5f + (MathF.Sin(time * 0.72f) * 0.5f);
        SKPoint marker = CubicPoint(markerAmount, bezierStart, firstControl, secondControl, bezierEnd);
        canvas.DrawCircle(marker, 8.0f, markerPaint);
        canvas.DrawCircle(marker, 8.0f, markerRingPaint);
    }

    protected override void DrawStatic(SKCanvas canvas, float width, float height)
    {
        using SKPaint paint = new() { IsAntialias = true };
        using SKShader background = SKShader.CreateLinearGradient(new(stage.Left, stage.Top), new(stage.Right, stage.Bottom), [new(28, 71, 78), new(31, 54, 75), new(72, 53, 66)], [0.0f, 0.58f, 1.0f], SKShaderTileMode.Clamp);

        paint.Shader = background;
        canvas.DrawRoundRect(stage, 6.0f, 6.0f, paint);
        paint.Shader = null;

        DrawGrid(canvas, paint);
        DrawStarConstruction(canvas, paint);
        DrawBezierConstruction(canvas, paint);
        DrawClipConstruction(canvas, paint);

        paint.Style = SKPaintStyle.Stroke;
        paint.StrokeWidth = 1.0f;
        paint.Color = new(198, 228, 226, 120);
        canvas.DrawRoundRect(stage, 6.0f, 6.0f, paint);
    }

    protected override void DisposeResources()
    {
        clipPath?.Dispose();
        markerRingPaint.Dispose();
        markerPaint.Dispose();
        dotPaint.Dispose();
        dashPaint.Dispose();
        ghostPaint.Dispose();
        outlinePaint.Dispose();
        shapePaint.Dispose();
        dashEffect.Dispose();
        shapeShader.Dispose();
        starPath.Dispose();
    }

    private void DrawTransformedStar(SKCanvas canvas, float time, float scale, float speed, SKPaint? fill, SKPaint outline)
    {
        canvas.Save();
        canvas.Translate(starCenter);
        canvas.RotateDegrees((time * speed) + (speed * 0.7f));
        canvas.Scale(scale * (1.0f + (MathF.Sin((time * 0.8f) + scale) * 0.025f)));

        if (fill is not null)
        {
            canvas.DrawPath(starPath, fill);
        }

        canvas.DrawPath(starPath, outline);
        canvas.Restore();
    }

    private void DrawClipField(SKCanvas canvas, float time)
    {
        canvas.Save();
        canvas.ClipPath(clipPath!, SKClipOperation.Intersect, true);

        int columns = Math.Clamp((int)(clipRect.Width / 31.0f), 7, 14);
        int rows = Math.Clamp((int)(clipRect.Height / 27.0f), 4, 10);
        float xStep = clipRect.Width / MathF.Max(1.0f, columns - 1.0f);
        float yStep = clipRect.Height / MathF.Max(1.0f, rows - 1.0f);

        for (int row = 0; row < rows; row++)
        {
            for (int column = 0; column < columns; column++)
            {
                float phase = (time * 1.35f) + (row * 0.55f) + (column * 0.34f);
                float x = clipRect.Left + (column * xStep) + (MathF.Cos(phase) * MathF.Min(4.0f, xStep * 0.12f));
                float y = clipRect.Top + (row * yStep) + (MathF.Sin(phase) * MathF.Min(5.0f, yStep * 0.17f));
                dotPaint.Color = DotColors[(row + column) % DotColors.Length];
                canvas.DrawCircle(x, y, 4.2f, dotPaint);
            }
        }

        canvas.Restore();
        canvas.DrawPath(clipPath!, outlinePaint);
    }

    private void DrawGrid(SKCanvas canvas, SKPaint paint)
    {
        float spacing = Math.Clamp(MathF.Min(stage.Width, stage.Height) / 16.0f, 24.0f, 38.0f);
        int verticalIndex = 0;

        paint.Style = SKPaintStyle.Stroke;
        paint.StrokeWidth = 1.0f;

        for (float x = stage.Left; x <= stage.Right; x += spacing)
        {
            paint.Color = verticalIndex % 4 is 0 ? new(190, 226, 225, 40) : new(190, 226, 225, 18);
            canvas.DrawLine(x, stage.Top, x, stage.Bottom, paint);
            verticalIndex++;
        }

        int horizontalIndex = 0;
        for (float y = stage.Top; y <= stage.Bottom; y += spacing)
        {
            paint.Color = horizontalIndex % 4 is 0 ? new(190, 226, 225, 40) : new(190, 226, 225, 18);
            canvas.DrawLine(stage.Left, y, stage.Right, y, paint);
            horizontalIndex++;
        }
    }

    private void DrawStarConstruction(SKCanvas canvas, SKPaint paint)
    {
        float radius = 72.0f * starScale;

        paint.Style = SKPaintStyle.Stroke;
        paint.StrokeWidth = 1.0f;
        paint.Color = new(205, 235, 232, 85);
        canvas.DrawCircle(starCenter, radius * 1.07f, paint);
        canvas.DrawCircle(starCenter, radius * 0.47f, paint);
        canvas.DrawLine(starCenter.X - (radius * 1.18f), starCenter.Y, starCenter.X + (radius * 1.18f), starCenter.Y, paint);
        canvas.DrawLine(starCenter.X, starCenter.Y - (radius * 1.18f), starCenter.X, starCenter.Y + (radius * 1.18f), paint);

        paint.Style = SKPaintStyle.Fill;
        paint.Color = new(220, 241, 238, 180);
        canvas.DrawText("TRANSFORM / 7-POINT PATH", starCenter.X - radius, starCenter.Y - (radius * 1.34f), SKTextAlign.Left, Resources.CaptionFont, paint);
    }

    private void DrawBezierConstruction(SKCanvas canvas, SKPaint paint)
    {
        paint.Style = SKPaintStyle.Stroke;
        paint.StrokeWidth = 1.0f;
        paint.Color = new(202, 232, 229, 105);
        canvas.DrawLine(bezierStart, firstControl, paint);
        canvas.DrawLine(secondControl, bezierEnd, paint);

        using SKPathBuilder builder = new();
        builder.MoveTo(bezierStart);
        builder.CubicTo(firstControl, secondControl, bezierEnd);
        using SKPath bezier = builder.Detach();

        paint.Color = new(242, 130, 117, 235);
        paint.StrokeWidth = 3.5f;
        paint.StrokeCap = SKStrokeCap.Round;
        canvas.DrawPath(bezier, paint);

        SKPoint[] handles = [bezierStart, firstControl, secondControl, bezierEnd];
        paint.Style = SKPaintStyle.Fill;
        for (int i = 0; i < handles.Length; i++)
        {
            paint.Color = i is 0 or 3 ? new(250, 246, 229) : new(106, 190, 226);
            canvas.DrawCircle(handles[i], i is 0 or 3 ? 4.5f : 3.5f, paint);
        }

        paint.Color = new(220, 241, 238, 180);
        canvas.DrawText("CUBIC TRAJECTORY", bezierStart.X, bezierStart.Y - 18.0f, SKTextAlign.Left, Resources.CaptionFont, paint);
    }

    private void DrawClipConstruction(SKCanvas canvas, SKPaint paint)
    {
        paint.Style = SKPaintStyle.Fill;
        paint.Color = new(232, 246, 243, 20);
        canvas.DrawRoundRect(clipRect, 18.0f, 18.0f, paint);

        paint.Color = new(220, 241, 238, 180);
        canvas.DrawText("CLIP WINDOW", clipRect.Left, clipRect.Top - 14.0f, SKTextAlign.Left, Resources.CaptionFont, paint);

        paint.Color = new(248, 199, 101, 180);
        canvas.DrawText("STROKE PHASE", waveRect.Left, waveRect.Top - 10.0f, SKTextAlign.Left, Resources.CaptionFont, paint);
    }

    private static SKPoint CubicPoint(float amount, SKPoint start, SKPoint first, SKPoint second, SKPoint end)
    {
        float inverse = 1.0f - amount;
        float inverseSquared = inverse * inverse;
        float amountSquared = amount * amount;

        return new(
            (inverseSquared * inverse * start.X) + (3.0f * inverseSquared * amount * first.X) + (3.0f * inverse * amountSquared * second.X) + (amountSquared * amount * end.X),
            (inverseSquared * inverse * start.Y) + (3.0f * inverseSquared * amount * first.Y) + (3.0f * inverse * amountSquared * second.Y) + (amountSquared * amount * end.Y));
    }

    private static SKPath CreateStar(float outerRadius, float innerRadius, int points)
    {
        using SKPathBuilder builder = new();

        for (int i = 0; i < points * 2; i++)
        {
            float angle = (-MathF.PI * 0.5f) + (i * MathF.PI / points);
            float radius = i % 2 is 0 ? outerRadius : innerRadius;
            float x = MathF.Cos(angle) * radius;
            float y = MathF.Sin(angle) * radius;

            if (i is 0)
            {
                builder.MoveTo(x, y);
            }
            else
            {
                builder.LineTo(x, y);
            }
        }

        builder.Close();
        return builder.Detach();
    }
}