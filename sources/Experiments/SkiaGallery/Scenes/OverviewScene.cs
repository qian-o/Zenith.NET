using SkiaSharp;

namespace SkiaGallery.Scenes;

internal class OverviewScene(GalleryResources resources) : GalleryScene(resources)
{
    private SKRect stage;

    public override string Navigation => "Overview";

    public override string Title => "Chromatic assembly";

    public override string Description => "Hard-edged geometry composed from color, scale, and overlap.";

    protected override void UpdateLayout(float width, float height)
    {
        stage = new(0.0f, 0.0f, width, height);
    }

    protected override void DrawStatic(SKCanvas canvas, float width, float height)
    {
        using SKPaint paint = new() { IsAntialias = true };

        paint.Color = new(246, 243, 235);
        canvas.DrawRoundRect(stage, 6.0f, 6.0f, paint);

        float margin = Math.Clamp(MathF.Min(width, height) * 0.06f, 18.0f, 38.0f);
        float gap = Math.Clamp(MathF.Min(width, height) * 0.022f, 8.0f, 15.0f);
        SKRect field = new(stage.Left + margin, stage.Top + margin, stage.Right - margin, stage.Bottom - margin);

        if (UseWideLayout(width, height))
        {
            DrawWideComposition(canvas, paint, field, gap);
        }
        else
        {
            DrawTallComposition(canvas, paint, field, gap);
        }

        paint.Style = SKPaintStyle.Stroke;
        paint.StrokeWidth = 1.0f;
        paint.Color = new(190, 187, 179);
        canvas.DrawRoundRect(stage, 6.0f, 6.0f, paint);
    }

    private static void DrawWideComposition(SKCanvas canvas, SKPaint paint, SKRect field, float gap)
    {
        float split = field.Left + (field.Width * 0.57f);
        SKRect dark = new(field.Left, field.Top, split - gap, field.Bottom);
        SKRect right = new(split, field.Top, field.Right, field.Bottom);
        float row = right.Top + (right.Height * 0.48f);
        float column = right.Left + (right.Width * 0.61f);

        DrawBlock(canvas, paint, dark, new(24, 39, 34));
        DrawBlock(canvas, paint, new(right.Left, right.Top, right.Right, row - gap), new(60, 116, 199));
        DrawBlock(canvas, paint, new(right.Left, row, column - gap, right.Bottom), new(224, 91, 82));
        DrawBlock(canvas, paint, new(column, row, right.Right, right.Top + (right.Height * 0.73f)), new(235, 174, 61));
        DrawBlock(canvas, paint, new(column, right.Top + (right.Height * 0.73f) + gap, right.Right, right.Bottom), new(35, 145, 108));

        DrawDarkFieldDetails(canvas, paint, dark, gap);
        DrawSquareAssembly(canvas, paint, new(dark.Left + (dark.Width * 0.64f), dark.MidY), MathF.Min(dark.Width, dark.Height) * 0.30f);
    }

    private static void DrawTallComposition(SKCanvas canvas, SKPaint paint, SKRect field, float gap)
    {
        float split = field.Top + (field.Height * 0.60f);
        SKRect dark = new(field.Left, field.Top, field.Right, split - gap);
        SKRect bottom = new(field.Left, split, field.Right, field.Bottom);
        float firstColumn = bottom.Left + (bottom.Width * 0.48f);
        float secondColumn = bottom.Left + (bottom.Width * 0.74f);

        DrawBlock(canvas, paint, dark, new(24, 39, 34));
        DrawBlock(canvas, paint, new(bottom.Left, bottom.Top, firstColumn - gap, bottom.Bottom), new(60, 116, 199));
        DrawBlock(canvas, paint, new(firstColumn, bottom.Top, secondColumn - gap, bottom.Bottom), new(224, 91, 82));
        DrawBlock(canvas, paint, new(secondColumn, bottom.Top, bottom.Right, bottom.MidY - (gap * 0.5f)), new(235, 174, 61));
        DrawBlock(canvas, paint, new(secondColumn, bottom.MidY + (gap * 0.5f), bottom.Right, bottom.Bottom), new(35, 145, 108));

        DrawDarkFieldDetails(canvas, paint, dark, gap);
        DrawSquareAssembly(canvas, paint, new(dark.MidX, dark.Top + (dark.Height * 0.48f)), MathF.Min(dark.Width, dark.Height) * 0.28f);
    }

    private static void DrawDarkFieldDetails(SKCanvas canvas, SKPaint paint, SKRect rect, float gap)
    {
        float barWidth = MathF.Max(3.0f, rect.Width * 0.012f);
        float startX = rect.Left + (gap * 1.6f);
        float bottom = rect.Bottom - (gap * 1.6f);

        for (int i = 0; i < 7; i++)
        {
            float height = rect.Height * (0.12f + (i * 0.045f));
            paint.Color = i % 3 is 0 ? new(235, 174, 61) : i % 3 is 1 ? new(224, 91, 82) : new(35, 145, 108);
            canvas.DrawRect(new(startX + (i * barWidth * 1.9f), bottom - height, startX + (i * barWidth * 1.9f) + barWidth, bottom), paint);
        }

        paint.Color = new(244, 240, 229, 45);
        canvas.DrawRect(new(rect.Left + (rect.Width * 0.08f), rect.Top + (rect.Height * 0.12f), rect.Left + (rect.Width * 0.36f), rect.Top + (rect.Height * 0.15f)), paint);
        canvas.DrawRect(new(rect.Left + (rect.Width * 0.08f), rect.Top + (rect.Height * 0.19f), rect.Left + (rect.Width * 0.27f), rect.Top + (rect.Height * 0.22f)), paint);
    }

    private static void DrawSquareAssembly(SKCanvas canvas, SKPaint paint, SKPoint center, float size)
    {
        canvas.Save();
        canvas.RotateDegrees(-9.0f, center.X, center.Y);
        paint.Color = new(246, 243, 235);
        canvas.DrawRect(new(center.X - size, center.Y - size, center.X + size, center.Y + size), paint);
        canvas.Restore();

        canvas.Save();
        canvas.RotateDegrees(11.0f, center.X, center.Y);
        paint.Color = new(60, 116, 199, 210);
        float inset = size * 0.38f;
        canvas.DrawRect(new(center.X - size + inset, center.Y - size + inset, center.X + size - inset, center.Y + size - inset), paint);
        canvas.Restore();
    }

    private static void DrawBlock(SKCanvas canvas, SKPaint paint, SKRect rect, SKColor color)
    {
        paint.Color = color;
        canvas.DrawRect(rect, paint);
    }
}