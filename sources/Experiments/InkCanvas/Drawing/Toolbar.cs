using SkiaSharp;
using Zenith.NET;

namespace InkCanvas.Drawing;

internal class Toolbar : DisposableObject
{
    public const float ToolbarHeight = 64.0f;

    public const float StatusHeight = 34.0f;

    private const float SwatchSize = 30.0f;
    private const float SwatchGap = 12.0f;
    private const float CheckboxSize = 18.0f;
    private const float MsaaWidth = 72.0f;
    private const float ButtonWidth = 64.0f;

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
    private readonly SKPaint fillPaint = new();
    private readonly SKPaint strokePaint = new() { Style = SKPaintStyle.Stroke };
    private readonly SKTextBlob? msaaLabel;
    private readonly SKTextBlob? clearLabel;
    private readonly SKTextBlob? hintLabel;

    private SKTextBlob? statistics;
    private SKRect msaaRect;
    private SKRect clearRect;
    private SKSize size;
    private int colorIndex;
    private int strokeWidthIndex = 1;

    public Toolbar()
    {
        const string ClearText = "CLEAR";
        const string HintText = "DRAG TO DRAW   RIGHT DRAG TO ERASE";

        string family;

        if (OperatingSystem.IsMacOS())
        {
            family = "SF Pro Text";
        }
        else if (OperatingSystem.IsWindows())
        {
            family = "Segoe UI";
        }
        else
        {
            family = "Noto Sans";
        }

        using SKTypeface typeface = SKTypeface.FromFamilyName(family, SKFontStyle.Normal);

        labelFont = new(typeface, 12.0f)
        {
            Subpixel = true,
            Edging = SKFontEdging.SubpixelAntialias,
            Hinting = SKFontHinting.Slight
        };

        msaaLabel = SKTextBlob.Create("MSAA", labelFont);
        clearLabel = SKTextBlob.Create(ClearText, labelFont, new(-labelFont.MeasureText(ClearText) * 0.5f, 0.0f));
        hintLabel = SKTextBlob.Create(HintText, labelFont, new(-labelFont.MeasureText(HintText), 0.0f));
    }

    public SKColor SelectedColor => Palette[colorIndex];

    public float SelectedStrokeWidth => StrokeWidths[strokeWidthIndex];

    public bool IsMsaaEnabled { get; private set; } = true;

    public void Resize(float width, float height)
    {
        const float Top = (ToolbarHeight - SwatchSize) * 0.5f;
        const float Bottom = Top + SwatchSize;

        size = new(width, height);

        for (int i = 0; i < swatchRects.Length; i++)
        {
            float left = SwatchGap + (i * (SwatchSize + SwatchGap));
            swatchRects[i] = new(left, Top, left + SwatchSize, Bottom);
        }

        float strokeWidthLeft = swatchRects[^1].Right + (SwatchGap * 2.0f);

        for (int i = 0; i < strokeWidthRects.Length; i++)
        {
            float left = strokeWidthLeft + (i * (SwatchSize + SwatchGap));
            strokeWidthRects[i] = new(left, Top, left + SwatchSize, Bottom);
        }

        float msaaLeft = strokeWidthRects[^1].Right + (SwatchGap * 2.0f);
        msaaRect = new(msaaLeft, Top, msaaLeft + MsaaWidth, Bottom);

        float clearLeft = MathF.Max(msaaRect.Right + (SwatchGap * 2.0f), width - SwatchGap - ButtonWidth);
        clearRect = new(clearLeft, Top, clearLeft + ButtonWidth, Bottom);
    }

    public void Draw(SKCanvas canvas, bool canClear)
    {
        DrawToolbar(canvas, canClear);
        DrawStatus(canvas);
    }

    public void SetStatistics(int strokeCount, int nodeCount)
    {
        statistics?.Dispose();
        statistics = null;
        statistics = SKTextBlob.Create($"STROKES {strokeCount}   NODES {nodeCount}", labelFont);
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
            IsMsaaEnabled = !IsMsaaEnabled;
        }
    }

    public bool IsClearButton(SKPoint position)
    {
        return clearRect.Contains(position.X, position.Y);
    }

    protected override void Destroy()
    {
        statistics?.Dispose();
        hintLabel?.Dispose();
        clearLabel?.Dispose();
        msaaLabel?.Dispose();
        labelFont?.Dispose();
        strokePaint?.Dispose();
        fillPaint?.Dispose();
    }

    private void DrawToolbar(SKCanvas canvas, bool canClear)
    {
        const float CheckboxTop = (ToolbarHeight - CheckboxSize) * 0.5f;

        fillPaint.Color = Panel;
        canvas.DrawRect(0.0f, 0.0f, size.Width, ToolbarHeight, fillPaint);

        fillPaint.Color = Divider;
        canvas.DrawRect(0.0f, ToolbarHeight - 1.0f, size.Width, 1.0f, fillPaint);

        for (int i = 0; i < Palette.Length; i++)
        {
            SKRect swatch = swatchRects[i];

            fillPaint.Color = Palette[i];
            canvas.DrawRoundRect(swatch, 6.0f, 6.0f, fillPaint);

            if (i == colorIndex)
            {
                strokePaint.Color = Highlight;
                strokePaint.StrokeWidth = 2.0f;
                canvas.DrawRoundRect(SKRect.Inflate(swatch, 4.0f, 4.0f), 9.0f, 9.0f, strokePaint);
            }
        }

        for (int i = 0; i < StrokeWidths.Length; i++)
        {
            SKRect slot = strokeWidthRects[i];

            fillPaint.Color = i == strokeWidthIndex ? Selected : Panel;
            canvas.DrawRoundRect(slot, 6.0f, 6.0f, fillPaint);

            fillPaint.Color = Palette[colorIndex];
            canvas.DrawCircle(slot.MidX, slot.MidY, StrokeWidths[i] * 0.5f, fillPaint);
        }

        SKRect checkbox = new(msaaRect.Left, CheckboxTop, msaaRect.Left + CheckboxSize, CheckboxTop + CheckboxSize);

        fillPaint.Color = IsMsaaEnabled ? Selected : Panel;
        canvas.DrawRoundRect(checkbox, 4.0f, 4.0f, fillPaint);

        strokePaint.Color = IsMsaaEnabled ? Highlight : Label;
        strokePaint.StrokeWidth = 1.5f;
        canvas.DrawRoundRect(checkbox, 4.0f, 4.0f, strokePaint);

        if (IsMsaaEnabled)
        {
            strokePaint.StrokeWidth = 2.0f;
            canvas.DrawLine(checkbox.Left + 4.0f, checkbox.MidY, checkbox.Left + 8.0f, checkbox.Bottom - 4.0f, strokePaint);
            canvas.DrawLine(checkbox.Left + 8.0f, checkbox.Bottom - 4.0f, checkbox.Right - 3.0f, checkbox.Top + 4.0f, strokePaint);
        }

        fillPaint.Color = Label;
        canvas.DrawText(msaaLabel, checkbox.Right + 8.0f, msaaRect.MidY + 4.0f, fillPaint);

        SKColor accent = canClear ? Label : Divider;

        fillPaint.Color = Panel;
        canvas.DrawRoundRect(clearRect, 6.0f, 6.0f, fillPaint);

        strokePaint.Color = accent;
        strokePaint.StrokeWidth = 1.5f;
        canvas.DrawRoundRect(clearRect, 6.0f, 6.0f, strokePaint);

        fillPaint.Color = accent;
        canvas.DrawText(clearLabel, clearRect.MidX, clearRect.MidY + 4.0f, fillPaint);
    }

    private void DrawStatus(SKCanvas canvas)
    {
        float top = size.Height - StatusHeight;

        fillPaint.Color = Panel;
        canvas.DrawRect(0.0f, top, size.Width, StatusHeight, fillPaint);

        fillPaint.Color = Divider;
        canvas.DrawRect(0.0f, top, size.Width, 1.0f, fillPaint);

        float baseline = top + (StatusHeight * 0.5f) + 4.0f;

        fillPaint.Color = Label;
        canvas.DrawText(statistics, SwatchGap, baseline, fillPaint);
        canvas.DrawText(hintLabel, size.Width - SwatchGap, baseline, fillPaint);
    }

    private static int IndexAt(ReadOnlySpan<SKRect> rects, SKPoint position)
    {
        for (int i = 0; i < rects.Length; i++)
        {
            if (rects[i].Contains(position.X, position.Y))
            {
                return i;
            }
        }

        return -1;
    }
}
