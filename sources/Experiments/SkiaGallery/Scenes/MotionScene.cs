using SkiaSharp;

namespace SkiaGallery.Scenes;

internal class MotionScene : GalleryScene
{
    private static readonly SKColor[] ParticleColors = [new(77, 151, 235, 220), new(52, 194, 146, 220), new(245, 112, 103, 220), new(247, 190, 80, 220)];

    private readonly Particle[] particles = new Particle[48];
    private readonly SKPoint[] firstRibbonPoints = new SKPoint[96];
    private readonly SKPoint[] secondRibbonPoints = new SKPoint[96];
    private readonly SKPoint[] thirdRibbonPoints = new SKPoint[96];
    private readonly SKShader firstShader;
    private readonly SKShader secondShader;
    private readonly SKPaint firstRibbonPaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 4.0f, StrokeCap = SKStrokeCap.Round };
    private readonly SKPaint secondRibbonPaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 3.0f, StrokeCap = SKStrokeCap.Round };
    private readonly SKPaint thirdRibbonPaint = new() { Color = new(122, 100, 186), IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 2.5f, StrokeCap = SKStrokeCap.Round };
    private readonly SKPaint ribbonGlowPaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 12.0f, StrokeCap = SKStrokeCap.Round };
    private readonly SKPaint orbitPaint = new() { Color = new(238, 242, 239, 38), IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1.0f };
    private readonly SKPaint particlePaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private readonly SKPaint corePaint = new() { Color = new(244, 246, 240), IsAntialias = true, Style = SKPaintStyle.Fill };

    private SKRect arenaRect;
    private SKRect waveRect;
    private SKPoint orbitCenter;
    private float orbitRadius;

    public MotionScene(GalleryResources resources) : base(resources)
    {
        for (int i = 0; i < particles.Length; i++)
        {
            particles[i] = new(
                0.38f + ((i % 6) * 0.105f),
                0.16f + ((i % 9) * 0.028f),
                i * 0.73f,
                1.5f + ((i % 3) * 0.75f),
                i % ParticleColors.Length);
        }

        firstShader = SKShader.CreateLinearGradient(new(0.0f, 0.0f), new(1400.0f, 0.0f), [GalleryPalette.Blue, GalleryPalette.Accent, GalleryPalette.Amber], SKShaderTileMode.Clamp);
        secondShader = SKShader.CreateLinearGradient(new(0.0f, 0.0f), new(1400.0f, 0.0f), [GalleryPalette.Coral, GalleryPalette.Amber, GalleryPalette.Blue], SKShaderTileMode.Clamp);
        firstRibbonPaint.Shader = firstShader;
        secondRibbonPaint.Shader = secondShader;
    }

    public override string Navigation => "Motion";

    public override string Title => "Kinetic field";

    public override string Description => "Polylines and a fixed particle pool moving across one GPU field.";

    public override bool IsAnimated => true;

    protected override void UpdateLayout(float width, float height)
    {
        arenaRect = new(0.0f, 0.0f, width, height);
        waveRect = new(-24.0f, 0.0f, width + 24.0f, height);

        if (UseWideLayout(width, height))
        {
            orbitCenter = new(width * 0.73f, height * 0.52f);
            orbitRadius = MathF.Min(width * 0.24f, height * 0.38f);
        }
        else
        {
            orbitCenter = new(width * 0.57f, height * 0.63f);
            orbitRadius = MathF.Min(width * 0.35f, height * 0.22f);
        }
    }

    protected override void DrawDynamic(SKCanvas canvas, float width, float height, double seconds)
    {
        float time = (float)seconds;
        bool wide = UseWideLayout(width, height);
        float firstCenter = height * (wide ? 0.28f : 0.22f);
        float secondCenter = height * (wide ? 0.52f : 0.43f);
        float thirdCenter = height * (wide ? 0.76f : 0.78f);

        UpdateRibbon(firstRibbonPoints, waveRect.Left, waveRect.Right, firstCenter, height * 0.075f, time * 1.4f, 13.0f);
        UpdateRibbon(secondRibbonPoints, waveRect.Left, waveRect.Right, secondCenter, height * 0.062f, (-time * 1.1f) + 2.0f, 17.0f);
        UpdateRibbon(thirdRibbonPoints, waveRect.Left, waveRect.Right, thirdCenter, height * 0.052f, (time * 0.8f) + 4.0f, 9.0f);

        canvas.Save();
        canvas.ClipRect(arenaRect);

        ribbonGlowPaint.Color = new(61, 126, 204, 32);
        canvas.DrawPoints(SKPointMode.Polygon, firstRibbonPoints, ribbonGlowPaint);
        ribbonGlowPaint.Color = new(229, 100, 90, 30);
        canvas.DrawPoints(SKPointMode.Polygon, secondRibbonPoints, ribbonGlowPaint);
        ribbonGlowPaint.Color = new(122, 100, 186, 28);
        canvas.DrawPoints(SKPointMode.Polygon, thirdRibbonPoints, ribbonGlowPaint);

        canvas.DrawPoints(SKPointMode.Polygon, firstRibbonPoints, firstRibbonPaint);
        canvas.DrawPoints(SKPointMode.Polygon, secondRibbonPoints, secondRibbonPaint);
        canvas.DrawPoints(SKPointMode.Polygon, thirdRibbonPoints, thirdRibbonPaint);

        canvas.Translate(orbitCenter);

        for (int i = 0; i < particles.Length; i++)
        {
            Particle particle = particles[i];
            float angle = particle.Phase + (time * particle.Speed);
            float radius = (particle.OrbitFactor * orbitRadius) + (MathF.Sin((time * 0.8f) + particle.Phase) * MathF.Min(5.0f, orbitRadius * 0.035f));
            float x = MathF.Cos(angle) * radius;
            float y = MathF.Sin(angle) * radius * 0.74f;

            particlePaint.Color = ParticleColors[particle.ColorIndex];
            canvas.DrawCircle(x, y, particle.Radius, particlePaint);
        }

        particlePaint.Color = new(244, 246, 240, 35);
        canvas.DrawCircle(0.0f, 0.0f, MathF.Max(17.0f, orbitRadius * 0.15f), particlePaint);
        canvas.DrawCircle(0.0f, 0.0f, MathF.Max(7.0f, orbitRadius * 0.055f) + (MathF.Sin(time * 2.0f) * 1.5f), corePaint);
        canvas.Restore();
    }

    protected override void DrawStatic(SKCanvas canvas, float width, float height)
    {
        using SKPaint paint = new() { IsAntialias = true };
        using SKShader background = SKShader.CreateLinearGradient(
            new(arenaRect.Left, arenaRect.Top),
            new(arenaRect.Right, arenaRect.Bottom),
            [new(13, 28, 25), new(28, 39, 48), new(40, 27, 39)],
            [0.0f, 0.58f, 1.0f],
            SKShaderTileMode.Clamp);

        paint.Shader = background;
        canvas.DrawRoundRect(arenaRect, 6.0f, 6.0f, paint);
        paint.Shader = null;

        paint.Color = new(238, 242, 239, 22);
        float stepX = width / 14.0f;
        float stepY = height / 9.0f;
        for (int row = 1; row < 9; row++)
        {
            for (int column = 1; column < 14; column++)
            {
                canvas.DrawCircle(column * stepX, row * stepY, 1.0f, paint);
            }
        }

        canvas.Save();
        canvas.Translate(orbitCenter);
        canvas.Scale(1.0f, 0.74f);
        for (int orbit = 1; orbit <= 6; orbit++)
        {
            canvas.DrawCircle(0.0f, 0.0f, orbitRadius * orbit / 6.0f, orbitPaint);
        }
        canvas.Restore();

        paint.Style = SKPaintStyle.Stroke;
        paint.StrokeWidth = 1.0f;
        paint.Color = new(238, 242, 239, 50);
        canvas.DrawRoundRect(arenaRect, 6.0f, 6.0f, paint);
    }

    protected override void DisposeResources()
    {
        corePaint.Dispose();
        particlePaint.Dispose();
        orbitPaint.Dispose();
        ribbonGlowPaint.Dispose();
        thirdRibbonPaint.Dispose();
        secondRibbonPaint.Dispose();
        firstRibbonPaint.Dispose();
        secondShader.Dispose();
        firstShader.Dispose();
    }

    private static void UpdateRibbon(SKPoint[] points, float left, float right, float centerY, float amplitude, float phase, float frequency)
    {
        for (int i = 0; i < points.Length; i++)
        {
            float amount = i / (points.Length - 1.0f);
            float y = centerY + (MathF.Sin((amount * frequency) + phase) * amplitude) + (MathF.Sin((amount * frequency * 2.3f) - (phase * 0.37f)) * amplitude * 0.18f);
            points[i] = new(left + (amount * (right - left)), y);
        }
    }

    private readonly record struct Particle(float OrbitFactor, float Speed, float Phase, float Radius, int ColorIndex);
}