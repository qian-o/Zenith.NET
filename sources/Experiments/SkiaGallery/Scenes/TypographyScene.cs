using SkiaSharp;

namespace SkiaGallery.Scenes;

internal class TypographyScene(GalleryResources resources) : GalleryScene(resources)
{
    private const float MaximumHeroSize = 94.0f;

    private static readonly string[] ScaleLabels = ["Display", "Title", "Section", "Body", "Caption"];
    private static readonly float[] ScaleFontSizes = [58.0f, 34.0f, 22.0f, 16.0f, 12.0f];

    private SKRect page;
    private SKRect masthead;
    private SKRect scaleColumn;
    private SKRect specimenArea;
    private SKRect pathArea;

    public override string Navigation => "Typography";

    public override string Title => "Editorial typography";

    public override string Description => "Hierarchy, metrics, path layout, and painted glyphs on one specimen page.";

    public override bool CanRender(float width, float height)
    {
        return base.CanRender(width, height) && (!UseWideLayout(width, height) || height >= 330.0f);
    }

    protected override void UpdateLayout(float width, float height)
    {
        page = new(0.0f, 0.0f, width, height);

        if (UseWideLayout(width, height))
        {
            float margin = Math.Clamp(width * 0.045f, 34.0f, 52.0f);
            float mastheadBottom = height * 0.46f;
            float columnWidth = width * 0.27f;

            masthead = new(margin, margin, width - margin, mastheadBottom);
            scaleColumn = new(margin, mastheadBottom + 22.0f, margin + columnWidth, height - margin);
            specimenArea = new(scaleColumn.Right + 34.0f, mastheadBottom + 22.0f, width - margin, height - margin);
            pathArea = new(specimenArea.Left, specimenArea.Top + (specimenArea.Height * 0.52f), specimenArea.Right, specimenArea.Bottom);
        }
        else
        {
            float margin = Math.Clamp(width * 0.06f, 24.0f, 34.0f);
            float mastheadBottom = height * 0.35f;
            float bodyTop = mastheadBottom + 18.0f;
            float scaleWidth = width * 0.36f;

            masthead = new(margin, margin, width - margin, mastheadBottom);
            scaleColumn = new(margin, bodyTop, margin + scaleWidth, height - margin);
            specimenArea = new(scaleColumn.Right + 22.0f, bodyTop, width - margin, height - margin);
            pathArea = new(specimenArea.Left, specimenArea.Top + (specimenArea.Height * 0.60f), specimenArea.Right, specimenArea.Bottom);
        }
    }

    protected override void DrawStatic(SKCanvas canvas, float width, float height)
    {
        using SKPaint paint = new() { IsAntialias = true };
        using SKPaint outline = new() { Color = GalleryPalette.Ink, IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1.4f };
        using SKMaskFilter blur = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 4.5f);
        using SKPaint shadow = new() { Color = new(25, 36, 32, 38), IsAntialias = true, MaskFilter = blur };
        using SKShader headlineShader = SKShader.CreateLinearGradient(new(masthead.Left, masthead.Top), new(masthead.Right, masthead.Bottom), [GalleryPalette.Blue, GalleryPalette.Accent, GalleryPalette.Coral, GalleryPalette.Amber], [0.0f, 0.38f, 0.72f, 1.0f], SKShaderTileMode.Clamp);

        paint.Color = new(251, 250, 247);
        canvas.DrawRoundRect(page, 4.0f, 4.0f, paint);

        DrawEditorialRules(canvas, paint);
        DrawMasthead(canvas, paint, headlineShader);
        DrawScaleColumn(canvas, paint);
        DrawSpecimen(canvas, paint, outline, shadow, headlineShader);
        DrawPathText(canvas, paint);

        paint.Style = SKPaintStyle.Stroke;
        paint.StrokeWidth = 1.0f;
        paint.Color = new(214, 211, 203);
        canvas.DrawRoundRect(page, 4.0f, 4.0f, paint);
    }

    private void DrawEditorialRules(SKCanvas canvas, SKPaint paint)
    {
        paint.Style = SKPaintStyle.Stroke;
        paint.StrokeWidth = 1.0f;
        paint.Color = new(214, 211, 203);
        canvas.DrawLine(masthead.Left, masthead.Top, masthead.Right, masthead.Top, paint);
        canvas.DrawLine(masthead.Left, masthead.Bottom, masthead.Right, masthead.Bottom, paint);
        canvas.DrawLine(scaleColumn.Right + 16.0f, scaleColumn.Top, scaleColumn.Right + 16.0f, scaleColumn.Bottom, paint);

        paint.Style = SKPaintStyle.Fill;
        paint.Color = GalleryPalette.Coral;
        canvas.DrawRect(masthead.Left, masthead.Top - 2.0f, MathF.Min(112.0f, masthead.Width * 0.18f), 4.0f, paint);
    }

    private void DrawMasthead(SKCanvas canvas, SKPaint paint, SKShader shader)
    {
        const string lead = "FORM";
        const string secondLine = "function.";
        const float minimumHeadingSize = 32.0f;
        const float maximumHeadingSize = 108.0f;

        float contentTop = masthead.Top + 48.0f;
        bool showNote = masthead.Height >= 230.0f;
        float contentBottom = masthead.Bottom - (showNote ? 34.0f : 14.0f);
        float availableHeight = MathF.Max(1.0f, contentBottom - contentTop);
        float headingSize = Math.Clamp(availableHeight / 2.18f, minimumHeadingSize, maximumHeadingSize);
        using SKFont headingFont = new(Resources.MediumTypeface, headingSize)
        {
            Edging = SKFontEdging.SubpixelAntialias,
            Hinting = SKFontHinting.Slight,
            Subpixel = true
        };

        float secondLineX = masthead.Left + (masthead.Width * 0.12f);
        float maximumTextWidth = masthead.Right - secondLineX;
        float secondLineWidth = headingFont.MeasureText(secondLine);

        if (secondLineWidth > maximumTextWidth)
        {
            headingSize *= maximumTextWidth / secondLineWidth;
            headingFont.Size = headingSize;
        }

        paint.Color = GalleryPalette.Muted;
        canvas.DrawText("SKIA TYPE SPECIMEN / VECTOR EDITION", masthead.Left, masthead.Top + 26.0f, SKTextAlign.Left, Resources.CaptionFont, paint);

        SKFontMetrics headingMetrics = headingFont.Metrics;
        float firstBaseline = contentTop - headingMetrics.Ascent;
        float lineGap = Math.Clamp(headingSize * 0.12f, 6.0f, 14.0f);
        float secondBaseline = firstBaseline + headingMetrics.Descent - headingMetrics.Ascent + lineGap;

        paint.Shader = shader;
        canvas.DrawText(lead, masthead.Left, firstBaseline, SKTextAlign.Left, headingFont, paint);
        paint.Shader = null;

        float followsSize = Math.Clamp(headingSize * 0.42f, 20.0f, Resources.TitleFont.Size);
        using SKFont followsFont = new(Resources.MediumTypeface, followsSize)
        {
            Edging = SKFontEdging.SubpixelAntialias,
            Hinting = SKFontHinting.Slight,
            Subpixel = true
        };
        float followsX = masthead.Left + headingFont.MeasureText(lead) + Math.Clamp(headingSize * 0.16f, 10.0f, 18.0f);
        float followsBaseline = firstBaseline - (headingSize * 0.08f);

        paint.Color = GalleryPalette.Ink;
        canvas.DrawText("follows", followsX, followsBaseline, SKTextAlign.Left, followsFont, paint);
        canvas.DrawText(secondLine, secondLineX, secondBaseline, SKTextAlign.Left, headingFont, paint);

        if (showNote)
        {
            paint.Color = GalleryPalette.Muted;
            string note = masthead.Width >= 680.0f ? "Scale, rhythm, and contour remain native vector geometry." : "Scale, rhythm, contour.";
            canvas.DrawText(note, masthead.Right, masthead.Bottom - 12.0f, SKTextAlign.Right, Resources.CaptionFont, paint);
        }
    }

    private void DrawScaleColumn(SKCanvas canvas, SKPaint paint)
    {
        paint.Color = GalleryPalette.Muted;
        canvas.DrawText("HIERARCHY", scaleColumn.Left, scaleColumn.Top + 17.0f, SKTextAlign.Left, Resources.CaptionFont, paint);

        float top = scaleColumn.Top + 42.0f;
        float availableHeight = scaleColumn.Bottom - top;
        float naturalHeight = 0.0f;

        for (int i = 0; i < ScaleFontSizes.Length; i++)
        {
            naturalHeight += ScaleFontSizes[i] * 1.12f;
        }

        float scale = MathF.Min(1.0f, availableHeight / naturalHeight);
        float baseline = top;

        for (int i = 0; i < ScaleLabels.Length; i++)
        {
            float fontSize = MathF.Max(10.0f, ScaleFontSizes[i] * scale);
            using SKFont font = new(i <= 2 ? Resources.MediumTypeface : Resources.RegularTypeface, fontSize)
            {
                Edging = SKFontEdging.SubpixelAntialias,
                Hinting = SKFontHinting.Slight,
                Subpixel = true
            };
            SKFontMetrics metrics = font.Metrics;
            baseline -= metrics.Ascent;

            paint.Color = i is 0 ? GalleryPalette.Coral : i is 1 ? GalleryPalette.Ink : GalleryPalette.Muted;
            canvas.DrawText(ScaleLabels[i], scaleColumn.Left, baseline, SKTextAlign.Left, font, paint);
            paint.Color = GalleryPalette.Muted;
            canvas.DrawText(((int)MathF.Round(fontSize)).ToString(), scaleColumn.Right, baseline, SKTextAlign.Right, Resources.CaptionFont, paint);

            baseline += metrics.Descent + (fontSize * 0.12f);
        }
    }

    private void DrawSpecimen(SKCanvas canvas, SKPaint paint, SKPaint outline, SKPaint shadow, SKShader shader)
    {
        paint.Color = GalleryPalette.Muted;
        canvas.DrawText("METRICS / PAINT", specimenArea.Left, specimenArea.Top + 17.0f, SKTextAlign.Left, Resources.CaptionFont, paint);

        float heroSize = Math.Clamp(MathF.Min(specimenArea.Width * 0.24f, specimenArea.Height * 0.42f), 38.0f, MaximumHeroSize);
        using SKFont heroFont = new(Resources.MediumTypeface, heroSize)
        {
            Edging = SKFontEdging.SubpixelAntialias,
            Hinting = SKFontHinting.Slight,
            Subpixel = true
        };
        float baseline = specimenArea.Top + MathF.Min(specimenArea.Height * 0.44f, 118.0f);
        float capLine = baseline - heroFont.Metrics.CapHeight;

        paint.Style = SKPaintStyle.Stroke;
        paint.StrokeWidth = 1.0f;
        paint.Color = GalleryPalette.Line;
        canvas.DrawLine(specimenArea.Left, capLine, specimenArea.Right, capLine, paint);
        canvas.DrawLine(specimenArea.Left, baseline, specimenArea.Right, baseline, paint);

        paint.Style = SKPaintStyle.Fill;
        paint.Shader = shader;
        canvas.DrawText("Aa", specimenArea.Left, baseline, SKTextAlign.Left, heroFont, paint);
        paint.Shader = null;

        float wordX = specimenArea.Left + heroFont.MeasureText("Aa") + Math.Clamp(specimenArea.Width * 0.04f, 12.0f, 22.0f);
        paint.Color = GalleryPalette.Ink;
        canvas.DrawText("VECTOR", wordX, baseline - 15.0f, SKTextAlign.Left, Resources.SectionFont, paint);
        canvas.DrawText("OUTLINE", wordX, baseline + 25.0f, SKTextAlign.Left, Resources.SectionFont, shadow);
        canvas.DrawText("OUTLINE", wordX, baseline + 25.0f, SKTextAlign.Left, Resources.SectionFont, outline);

        paint.Color = GalleryPalette.Muted;
        canvas.DrawText("CAP", specimenArea.Right, capLine - 5.0f, SKTextAlign.Right, Resources.CaptionFont, paint);
        canvas.DrawText("BASELINE", specimenArea.Right, baseline - 5.0f, SKTextAlign.Right, Resources.CaptionFont, paint);
    }

    private void DrawPathText(SKCanvas canvas, SKPaint paint)
    {
        SKPoint start = new(pathArea.Left, pathArea.Bottom - 18.0f);
        SKPoint end = new(pathArea.Right, pathArea.Top + 36.0f);
        SKPoint first = new(pathArea.Left + (pathArea.Width * 0.32f), pathArea.Top + 8.0f);
        SKPoint second = new(pathArea.Left + (pathArea.Width * 0.70f), pathArea.Bottom - 3.0f);

        using SKPathBuilder builder = new();
        builder.MoveTo(start);
        builder.CubicTo(first, second, end);
        using SKPath path = builder.Detach();

        paint.Style = SKPaintStyle.Stroke;
        paint.StrokeWidth = 1.0f;
        paint.Color = GalleryPalette.Line;
        canvas.DrawLine(start, first, paint);
        canvas.DrawLine(second, end, paint);

        paint.Style = SKPaintStyle.Fill;
        paint.Color = GalleryPalette.Accent;
        string sample = pathArea.Width >= 360.0f ? "TYPE FOLLOWS A REUSABLE CUBIC PATH" : "TYPE FOLLOWS PATH";
        canvas.DrawTextOnPath(sample, path, 6.0f, -8.0f, SKTextAlign.Left, Resources.BodyFont, paint);

        paint.Color = GalleryPalette.Blue;
        canvas.DrawCircle(first, 3.5f, paint);
        canvas.DrawCircle(second, 3.5f, paint);
    }
}