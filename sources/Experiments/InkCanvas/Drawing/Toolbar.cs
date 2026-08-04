using SkiaSharp;

namespace InkCanvas.Drawing;

internal class Toolbar : IDisposable
{
    public const float ToolbarHeight = 64.0f;
    public const float StatusHeight = 34.0f;

    private const float SwatchSize = 30.0f;
    private const float SwatchGap = 12.0f;
    private const float ButtonWidth = 64.0f;
    private const float CheckboxSize = 18.0f;
    private const float MSAAWidth = 72.0f;

    private static readonly SKColor Panel = new(31, 34, 43);
    private static readonly SKColor Divider = new(48, 52, 63);
    private static readonly SKColor Label = new(150, 158, 172);
    private static readonly SKColor Highlight = new(238, 242, 248);
    private static readonly SKColor Selected = new(58, 64, 78);

    private static readonly SKColor[] Palette =
    [
        new(238, 242, 248),
        new(236, 108, 96),
        new(238, 178, 74),
        new(96, 196, 154),
        new(102, 158, 240),
        new(178, 134, 234)
    ];

    private static readonly float[] StrokeWidths = [2.0f, 4.0f, 8.0f, 16.0f];

    private readonly SKRect[] swatchRects = new SKRect[Palette.Length];
    private readonly SKRect[] strokeWidthRects = new SKRect[StrokeWidths.Length];

    private readonly SKFont labelFont;
    private readonly SKPaint fillPaint = new() { IsAntialias = true };
    private readonly SKPaint strokePaint = new()
    {
        IsAntialias = true,
        Style = SKPaintStyle.Stroke
    };

    private SKRect msaaRect;
    private SKRect clearRect;
    private SKSize size;

    private int colorIndex;
    private int strokeWidthIndex = 1;

    public Toolbar()
    {
        string family = OperatingSystem.IsMacOS() ? "SF Pro Text" : OperatingSystem.IsWindows() ? "Segoe UI" : "Noto Sans";

        using SKTypeface typeface = SKTypeface.FromFamilyName(family, SKFontStyle.Normal);

        labelFont = new(typeface, 12.0f)
        {
            Subpixel = true,
            Edging = SKFontEdging.SubpixelAntialias,
            Hinting = SKFontHinting.Slight
        };
    }

    public SKColor SelectedColor => Palette[colorIndex];

    public float SelectedStrokeWidth => StrokeWidths[strokeWidthIndex];

    public bool MSAA { get; private set; } = true;

    public void Resize(float width, float height)
    {
        const float top = (ToolbarHeight - SwatchSize) * 0.5f;
        const float bottom = top + SwatchSize;

        size = new(width, height);

        for (int index = 0; index < swatchRects.Length; index++)
        {
            float left = SwatchGap + (index * (SwatchSize + SwatchGap));
            swatchRects[index] = new(left, top, left + SwatchSize, bottom);
        }

        float strokeWidthLeft = swatchRects[^1].Right + (SwatchGap * 2.0f);

        for (int index = 0; index < strokeWidthRects.Length; index++)
        {
            float left = strokeWidthLeft + (index * (SwatchSize + SwatchGap));
            strokeWidthRects[index] = new(left, top, left + SwatchSize, bottom);
        }

        float msaaLeft = strokeWidthRects[^1].Right + (SwatchGap * 2.0f);
        msaaRect = new(msaaLeft, top, msaaLeft + MSAAWidth, bottom);

        float clearLeft = MathF.Max(msaaRect.Right + (SwatchGap * 2.0f), width - SwatchGap - ButtonWidth);
        clearRect = new(clearLeft, top, clearLeft + ButtonWidth, bottom);
    }

    public void Draw(SKCanvas canvas, int strokeCount, int nodeCount, bool canClear)
    {
        DrawToolbar(canvas, canClear);
        DrawStatus(canvas, strokeCount, nodeCount);
    }

    public bool IsClearButton(SKPoint position)
    {
        return clearRect.Contains(position.X, position.Y);
    }

    public void SelectAt(SKPoint position)
    {
        int swatch = IndexAt(swatchRects, position);
        int strokeWidth = IndexAt(strokeWidthRects, position);

        if (swatch >= 0)
        {
            colorIndex = swatch;
        }
        else if (strokeWidth >= 0)
        {
            strokeWidthIndex = strokeWidth;
        }
        else if (msaaRect.Contains(position.X, position.Y))
        {
            MSAA = !MSAA;
        }
    }

    public void Dispose()
    {
        strokePaint.Dispose();
        fillPaint.Dispose();
        labelFont.Dispose();
    }

    private void DrawToolbar(SKCanvas canvas, bool canClear)
    {
        const float checkboxTop = (ToolbarHeight - CheckboxSize) * 0.5f;

        fillPaint.Color = Panel;
        canvas.DrawRect(0.0f, 0.0f, size.Width, ToolbarHeight, fillPaint);

        fillPaint.Color = Divider;
        canvas.DrawRect(0.0f, ToolbarHeight - 1.0f, size.Width, 1.0f, fillPaint);

        for (int index = 0; index < Palette.Length; index++)
        {
            SKRect swatch = swatchRects[index];

            fillPaint.Color = Palette[index];
            canvas.DrawRoundRect(swatch, 6.0f, 6.0f, fillPaint);

            if (index == colorIndex)
            {
                strokePaint.Color = Highlight;
                strokePaint.StrokeWidth = 2.0f;
                canvas.DrawRoundRect(SKRect.Inflate(swatch, 4.0f, 4.0f), 9.0f, 9.0f, strokePaint);
            }
        }

        for (int index = 0; index < StrokeWidths.Length; index++)
        {
            SKRect slot = strokeWidthRects[index];

            fillPaint.Color = index == strokeWidthIndex ? Selected : Panel;
            canvas.DrawRoundRect(slot, 6.0f, 6.0f, fillPaint);

            fillPaint.Color = Palette[colorIndex];
            canvas.DrawCircle(slot.MidX, slot.MidY, StrokeWidths[index] * 0.5f, fillPaint);
        }

        SKRect checkbox = new(msaaRect.Left, checkboxTop, msaaRect.Left + CheckboxSize, checkboxTop + CheckboxSize);

        fillPaint.Color = MSAA ? Selected : Panel;
        canvas.DrawRoundRect(checkbox, 4.0f, 4.0f, fillPaint);

        strokePaint.Color = MSAA ? Highlight : Label;
        strokePaint.StrokeWidth = 1.5f;
        canvas.DrawRoundRect(checkbox, 4.0f, 4.0f, strokePaint);

        if (MSAA)
        {
            strokePaint.StrokeWidth = 2.0f;
            canvas.DrawLine(checkbox.Left + 4.0f, checkbox.MidY, checkbox.Left + 8.0f, checkbox.Bottom - 4.0f, strokePaint);
            canvas.DrawLine(checkbox.Left + 8.0f, checkbox.Bottom - 4.0f, checkbox.Right - 3.0f, checkbox.Top + 4.0f, strokePaint);
        }

        fillPaint.Color = Label;
        canvas.DrawText("MSAA", checkbox.Right + 8.0f, msaaRect.MidY + 4.0f, SKTextAlign.Left, labelFont, fillPaint);

        SKColor accent = canClear ? Label : Divider;

        fillPaint.Color = Panel;
        canvas.DrawRoundRect(clearRect, 6.0f, 6.0f, fillPaint);

        strokePaint.Color = accent;
        strokePaint.StrokeWidth = 1.5f;
        canvas.DrawRoundRect(clearRect, 6.0f, 6.0f, strokePaint);

        fillPaint.Color = accent;
        canvas.DrawText("CLEAR", clearRect.MidX, clearRect.MidY + 4.0f, SKTextAlign.Center, labelFont, fillPaint);
    }

    private void DrawStatus(SKCanvas canvas, int strokeCount, int nodeCount)
    {
        float top = size.Height - StatusHeight;

        fillPaint.Color = Panel;
        canvas.DrawRect(0.0f, top, size.Width, StatusHeight, fillPaint);

        fillPaint.Color = Divider;
        canvas.DrawRect(0.0f, top, size.Width, 1.0f, fillPaint);

        float baseline = top + (StatusHeight * 0.5f) + 4.0f;

        fillPaint.Color = Label;
        canvas.DrawText($"STROKES {strokeCount}   NODES {nodeCount}", SwatchGap, baseline, SKTextAlign.Left, labelFont, fillPaint);
        canvas.DrawText("DRAG TO DRAW   RIGHT DRAG TO ERASE", size.Width - SwatchGap, baseline, SKTextAlign.Right, labelFont, fillPaint);
    }

    private static int IndexAt(SKRect[] rects, SKPoint position)
    {
        for (int index = 0; index < rects.Length; index++)
        {
            if (rects[index].Contains(position.X, position.Y))
            {
                return index;
            }
        }

        return -1;
    }
}
