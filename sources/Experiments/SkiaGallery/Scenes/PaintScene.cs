using SkiaSharp;

namespace SkiaGallery;

internal sealed class PaintScene : GalleryScene
{
    private readonly SKBitmap bitmap;
    private readonly SKImage image;

    private SKRect canvasRect;
    private SKRect spectrumRect;
    private SKRect blendRect;
    private SKRect blurRect;
    private SKRect imageRect;

    public PaintScene(GalleryResources resources) : base(resources)
    {
        bitmap = CreateBitmap();
        image = SKImage.FromBitmap(bitmap);
    }

    public override string Navigation => "Paint & image";

    public override string Title => "Color laboratory";

    public override string Description => "Shaders, blend modes, blur, sampling, and color transforms in one study.";

    protected override void UpdateLayout(float width, float height)
    {
        canvasRect = new(0.0f, 0.0f, width, height);

        if (UseWideLayout(width, height))
        {
            float split = width * 0.63f;
            spectrumRect = new(0.0f, 0.0f, split, height * 0.58f);
            blendRect = new(split, 0.0f, width, height * 0.58f);
            blurRect = new(0.0f, height * 0.58f, width * 0.38f, height);
            imageRect = new(width * 0.38f, height * 0.58f, width, height);
        }
        else
        {
            spectrumRect = new(0.0f, 0.0f, width, height * 0.38f);
            blendRect = new(0.0f, height * 0.38f, width * 0.48f, height * 0.68f);
            blurRect = new(width * 0.48f, height * 0.38f, width, height * 0.68f);
            imageRect = new(0.0f, height * 0.68f, width, height);
        }
    }

    protected override void DrawStatic(SKCanvas canvas, float width, float height)
    {
        using SKPaint paint = new() { IsAntialias = true };
        using SKPaint imagePaint = new() { IsAntialias = true };
        using SKPaint filteredPaint = new() { IsAntialias = true };
        using SKMaskFilter blur = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 16.0f);
        using SKColorFilter colorFilter = SKColorFilter.CreateColorMatrix(
        [
            0.72f, 0.12f, 0.16f, 0.0f, 18.0f / 255.0f,
            0.05f, 0.82f, 0.13f, 0.0f, 4.0f / 255.0f,
            0.14f, 0.16f, 0.70f, 0.0f, 12.0f / 255.0f,
            0.0f, 0.0f, 0.0f, 1.0f, 0.0f
        ]);

        filteredPaint.ColorFilter = colorFilter;
        SKSamplingOptions sampling = new(SKCubicResampler.Mitchell);

        paint.Color = new(246, 245, 241);
        canvas.DrawRoundRect(canvasRect, 5.0f, 5.0f, paint);

        DrawSpectrum(canvas, paint);
        DrawBlendStudy(canvas, paint);
        DrawBlurStudy(canvas, paint, blur);
        DrawImageStudy(canvas, paint, imagePaint, filteredPaint, sampling);
        DrawDividers(canvas, paint);

        paint.Style = SKPaintStyle.Stroke;
        paint.StrokeWidth = 1.0f;
        paint.Color = new(208, 207, 201);
        canvas.DrawRoundRect(canvasRect, 5.0f, 5.0f, paint);
    }

    protected override void DisposeResources()
    {
        image.Dispose();
        bitmap.Dispose();
    }

    private void DrawSpectrum(SKCanvas canvas, SKPaint paint)
    {
        SKRect stage = Inset(spectrumRect, 28.0f, 54.0f, 28.0f, 28.0f);
        using SKShader baseGradient = SKShader.CreateLinearGradient(
            new(stage.Left, stage.Top),
            new(stage.Right, stage.Top),
            [new(43, 90, 176), new(47, 168, 154), new(239, 190, 73), new(226, 91, 94), new(119, 77, 165)],
            [0.0f, 0.26f, 0.52f, 0.76f, 1.0f],
            SKShaderTileMode.Clamp);
        using SKShader lightGradient = SKShader.CreateLinearGradient(
            new(stage.Left, stage.Top),
            new(stage.Left, stage.Bottom),
            [new(255, 255, 255, 18), new(255, 255, 255, 185), new(23, 31, 29, 80)],
            [0.0f, 0.56f, 1.0f],
            SKShaderTileMode.Clamp);

        paint.Shader = baseGradient;
        canvas.DrawRect(stage, paint);
        paint.Shader = lightGradient;
        paint.BlendMode = SKBlendMode.Screen;
        canvas.DrawRect(stage, paint);
        paint.BlendMode = SKBlendMode.SrcOver;
        paint.Shader = null;

        paint.Color = GalleryPalette.Ink;
        canvas.DrawText("SPECTRUM", spectrumRect.Left + 28.0f, spectrumRect.Top + 28.0f, SKTextAlign.Left, Resources.CaptionFont, paint);
        paint.Color = GalleryPalette.Muted;
        canvas.DrawText("LINEAR SHADER / FIVE STOPS", spectrumRect.Right - 28.0f, spectrumRect.Top + 28.0f, SKTextAlign.Right, Resources.CaptionFont, paint);

        paint.Color = new(255, 255, 255, 125);
        paint.Style = SKPaintStyle.Stroke;
        paint.StrokeWidth = 1.0f;
        for (int i = 1; i < 5; i++)
        {
            float x = stage.Left + (stage.Width * i / 5.0f);
            canvas.DrawLine(x, stage.Top, x, stage.Bottom, paint);
        }

        paint.Style = SKPaintStyle.Fill;
    }

    private void DrawBlendStudy(SKCanvas canvas, SKPaint paint)
    {
        SKRect stage = Inset(blendRect, 24.0f, 54.0f, 24.0f, 24.0f);
        float radius = MathF.Min(stage.Width, stage.Height) * 0.28f;
        SKPoint center = new(stage.MidX, stage.MidY + 8.0f);

        paint.Color = GalleryPalette.Ink;
        canvas.DrawText("BLEND", blendRect.Left + 24.0f, blendRect.Top + 28.0f, SKTextAlign.Left, Resources.CaptionFont, paint);

        paint.Color = new(229, 100, 90, 175);
        canvas.DrawCircle(center.X - (radius * 0.42f), center.Y, radius, paint);
        paint.Color = new(61, 126, 204, 175);
        paint.BlendMode = SKBlendMode.Plus;
        canvas.DrawCircle(center.X + (radius * 0.42f), center.Y, radius, paint);
        paint.BlendMode = SKBlendMode.SrcOver;

        paint.Color = GalleryPalette.Muted;
        canvas.DrawText("PLUS", blendRect.Right - 24.0f, blendRect.Bottom - 20.0f, SKTextAlign.Right, Resources.CaptionFont, paint);
    }

    private void DrawBlurStudy(SKCanvas canvas, SKPaint paint, SKMaskFilter blur)
    {
        SKRect stage = Inset(blurRect, 24.0f, 52.0f, 24.0f, 24.0f);
        float radius = MathF.Min(stage.Width, stage.Height) * 0.22f;

        paint.Color = GalleryPalette.Ink;
        canvas.DrawText("BLUR FIELD", blurRect.Left + 24.0f, blurRect.Top + 28.0f, SKTextAlign.Left, Resources.CaptionFont, paint);

        paint.MaskFilter = blur;
        paint.Color = new(37, 153, 116, 128);
        canvas.DrawCircle(stage.MidX - (radius * 0.55f), stage.MidY, radius, paint);
        paint.Color = new(231, 168, 66, 128);
        canvas.DrawCircle(stage.MidX + (radius * 0.55f), stage.MidY, radius, paint);
        paint.MaskFilter = null;

        paint.Color = GalleryPalette.Ink;
        canvas.DrawCircle(stage.MidX, stage.MidY, 4.0f, paint);
    }

    private void DrawImageStudy(SKCanvas canvas, SKPaint paint, SKPaint imagePaint, SKPaint filteredPaint, SKSamplingOptions sampling)
    {
        SKRect stage = Inset(imageRect, 24.0f, 52.0f, 24.0f, 24.0f);
        SKRect left = new(stage.Left, stage.Top, stage.MidX - 4.0f, stage.Bottom);
        SKRect right = new(stage.MidX + 4.0f, stage.Top, stage.Right, stage.Bottom);
        SKRect source = new(0.0f, 0.0f, bitmap.Width, bitmap.Height);

        paint.Color = GalleryPalette.Ink;
        canvas.DrawText("IMAGE TRANSFORM", imageRect.Left + 24.0f, imageRect.Top + 28.0f, SKTextAlign.Left, Resources.CaptionFont, paint);
        paint.Color = GalleryPalette.Muted;
        canvas.DrawText("ORIGINAL / 4 × 5 MATRIX", imageRect.Right - 24.0f, imageRect.Top + 28.0f, SKTextAlign.Right, Resources.CaptionFont, paint);

        canvas.DrawImage(image, source, left, sampling, imagePaint);
        canvas.DrawImage(image, source, right, sampling, filteredPaint);

        paint.Color = new(255, 255, 255, 180);
        canvas.DrawRect(new(stage.MidX - 1.0f, stage.Top, stage.MidX + 1.0f, stage.Bottom), paint);
    }

    private void DrawDividers(SKCanvas canvas, SKPaint paint)
    {
        paint.Style = SKPaintStyle.Stroke;
        paint.StrokeWidth = 1.0f;
        paint.Color = new(208, 207, 201);

        if (spectrumRect.Right < canvasRect.Right)
        {
            canvas.DrawLine(spectrumRect.Right, canvasRect.Top, spectrumRect.Right, spectrumRect.Bottom, paint);
        }

        canvas.DrawLine(blurRect.Left, blurRect.Top, canvasRect.Right, blurRect.Top, paint);

        if (blurRect.Right < canvasRect.Right)
        {
            canvas.DrawLine(blurRect.Right, blurRect.Top, blurRect.Right, canvasRect.Bottom, paint);
        }

        paint.Style = SKPaintStyle.Fill;
    }

    private static SKRect Inset(SKRect rect, float left, float top, float right, float bottom)
    {
        return new(rect.Left + left, rect.Top + top, rect.Right - right, rect.Bottom - bottom);
    }

    private static SKBitmap CreateBitmap()
    {
        SKBitmap result = new(240, 160, SKColorType.Bgra8888, SKAlphaType.Premul);

        for (int y = 0; y < result.Height; y++)
        {
            for (int x = 0; x < result.Width; x++)
            {
                float horizontal = x / (result.Width - 1.0f);
                float vertical = y / (result.Height - 1.0f);
                byte red = (byte)(42.0f + (horizontal * 185.0f));
                byte green = (byte)(74.0f + ((1.0f - vertical) * 126.0f));
                byte blue = (byte)(108.0f + (MathF.Sin((horizontal + vertical) * 6.0f) * 52.0f));
                result.SetPixel(x, y, new(red, green, blue));
            }
        }

        return result;
    }
}