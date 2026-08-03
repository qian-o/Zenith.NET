using SkiaSharp;

namespace InkCanvas;

internal sealed class Board : IDisposable
{
    private const float ToolbarHeight = 64.0f;
    private const float StatusHeight = 34.0f;
    private const float SwatchSize = 30.0f;
    private const float SwatchGap = 12.0f;
    private const float ButtonWidth = 64.0f;
    private const float EraserRadius = 22.0f;

    private static readonly SKColor Surface = new(22, 24, 30);
    private static readonly SKColor Panel = new(31, 34, 43);
    private static readonly SKColor Divider = new(48, 52, 63);
    private static readonly SKColor Label = new(150, 158, 172);
    private static readonly SKColor Highlight = new(238, 242, 248);
    private static readonly SKColor Grid = new(32, 35, 43);
    private static readonly SKColor Selected = new(58, 64, 78);
    private static readonly SKColor Cursor = new(150, 158, 172, 200);

    private static readonly SKColor[] Palette =
    [
        new(238, 242, 248),
        new(236, 108, 96),
        new(238, 178, 74),
        new(96, 196, 154),
        new(102, 158, 240),
        new(178, 134, 234)
    ];

    private static readonly float[] Widths = [2.0f, 4.0f, 8.0f, 16.0f];

    private readonly List<Stroke> strokes = [];
    private readonly SKRect[] swatchRects = new SKRect[Palette.Length];
    private readonly SKRect[] widthRects = new SKRect[Widths.Length];
    private readonly SKTypeface typeface;
    private readonly SKFont labelFont;
    private readonly SKPaint fillPaint = new() { IsAntialias = true };
    private readonly SKPaint strokePaint = new()
    {
        IsAntialias = true,
        Style = SKPaintStyle.Stroke,
        StrokeCap = SKStrokeCap.Round,
        StrokeJoin = SKStrokeJoin.Round
    };

    private SKPicture? bakedStrokes;
    private Stroke? activeStroke;
    private SKRect stage;
    private SKRect canvasArea;
    private SKRect eraserRect;
    private SKRect clearRect;
    private SKPoint pointer;
    private SKPoint eraserLast;
    private int colorIndex;
    private int widthIndex = 1;
    private bool eraserMode;
    private bool erasing;
    private bool pointerInside;
    private bool bakeDirty = true;
    private float layoutWidth = -1.0f;
    private float layoutHeight = -1.0f;

    public Board()
    {
        string family = OperatingSystem.IsMacOS() ? "SF Pro Text" : OperatingSystem.IsWindows() ? "Segoe UI" : "Noto Sans";

        typeface = SKTypeface.FromFamilyName(family, SKFontStyle.Normal);
        labelFont = new(typeface, 12.0f)
        {
            Edging = SKFontEdging.SubpixelAntialias,
            Hinting = SKFontHinting.Slight,
            Subpixel = true
        };
    }

    public void Draw(SKCanvas canvas, float width, float height)
    {
        EnsureLayout(width, height);

        fillPaint.Color = Surface;
        canvas.DrawRect(stage, fillPaint);

        DrawGrid(canvas);

        if (bakeDirty)
        {
            bakedStrokes?.Dispose();
            bakedStrokes = BakeStrokes();
            bakeDirty = false;
        }

        canvas.Save();
        canvas.ClipRect(canvasArea);
        canvas.DrawPicture(bakedStrokes!);

        if (activeStroke is not null)
        {
            DrawStroke(canvas, activeStroke);
        }

        if ((eraserMode || erasing) && pointerInside)
        {
            DrawEraserCursor(canvas);
        }

        canvas.Restore();

        DrawToolbar(canvas);
        DrawStatus(canvas);
    }

    public void PointerMove(SKPoint position)
    {
        pointer = position;
        pointerInside = canvasArea.Contains(position.X, position.Y);

        if (erasing)
        {
            Erase(eraserLast, position);
            eraserLast = position;
        }
        else
        {
            activeStroke?.Add(position);
        }
    }

    public void PointerDown(SKPoint position, bool erase)
    {
        pointer = position;

        if (position.Y <= ToolbarHeight)
        {
            HandleToolbarClick(position);

            return;
        }

        if (!canvasArea.Contains(position.X, position.Y))
        {
            return;
        }

        pointerInside = true;

        if (erase || eraserMode)
        {
            erasing = true;
            eraserLast = position;
            Erase(position, position);

            return;
        }

        activeStroke = new(Palette[colorIndex], Widths[widthIndex]);
        activeStroke.Add(position);
    }

    public void PointerUp()
    {
        erasing = false;

        if (activeStroke is null)
        {
            return;
        }

        strokes.Add(activeStroke);
        activeStroke = null;
        bakeDirty = true;
    }

    public void Clear()
    {
        if (strokes.Count is 0)
        {
            return;
        }

        foreach (Stroke stroke in strokes)
        {
            stroke.Dispose();
        }

        strokes.Clear();
        bakeDirty = true;
    }

    public void SelectColor(int index)
    {
        colorIndex = Math.Clamp(index, 0, Palette.Length - 1);
        eraserMode = false;
    }

    public void SelectWidth(int index)
    {
        widthIndex = Math.Clamp(index, 0, Widths.Length - 1);
    }

    public void ToggleEraser()
    {
        eraserMode = !eraserMode;
    }

    public void Dispose()
    {
        foreach (Stroke stroke in strokes)
        {
            stroke.Dispose();
        }

        strokes.Clear();
        activeStroke?.Dispose();
        bakedStrokes?.Dispose();
        strokePaint.Dispose();
        fillPaint.Dispose();
        labelFont.Dispose();
        typeface.Dispose();
    }

    private void EnsureLayout(float width, float height)
    {
        const float top = (ToolbarHeight - SwatchSize) * 0.5f;
        const float bottom = top + SwatchSize;

        if (width == layoutWidth && height == layoutHeight)
        {
            return;
        }

        layoutWidth = width;
        layoutHeight = height;
        stage = new(0.0f, 0.0f, width, height);
        canvasArea = new(0.0f, ToolbarHeight, width, MathF.Max(ToolbarHeight, height - StatusHeight));

        for (int index = 0; index < swatchRects.Length; index++)
        {
            float left = SwatchGap + (index * (SwatchSize + SwatchGap));
            swatchRects[index] = new(left, top, left + SwatchSize, bottom);
        }

        float widthLeft = swatchRects[^1].Right + (SwatchGap * 2.0f);

        for (int index = 0; index < widthRects.Length; index++)
        {
            float left = widthLeft + (index * (SwatchSize + SwatchGap));
            widthRects[index] = new(left, top, left + SwatchSize, bottom);
        }

        float eraserLeft = widthRects[^1].Right + (SwatchGap * 2.0f);
        eraserRect = new(eraserLeft, top, eraserLeft + ButtonWidth, bottom);

        float clearLeft = MathF.Max(eraserRect.Right + (SwatchGap * 2.0f), width - SwatchGap - ButtonWidth);
        clearRect = new(clearLeft, top, clearLeft + ButtonWidth, bottom);
    }

    private SKPicture BakeStrokes()
    {
        using SKPictureRecorder recorder = new();
        SKCanvas canvas = recorder.BeginRecording(canvasArea);

        foreach (Stroke stroke in strokes)
        {
            DrawStroke(canvas, stroke);
        }

        return recorder.EndRecording();
    }

    private void DrawStroke(SKCanvas canvas, Stroke stroke)
    {
        strokePaint.Color = stroke.Color;
        strokePaint.StrokeWidth = stroke.Width;
        canvas.DrawPath(stroke.Path, strokePaint);
    }

    private void DrawGrid(SKCanvas canvas)
    {
        const float spacing = 32.0f;

        fillPaint.Color = Grid;

        for (float x = spacing; x < canvasArea.Right; x += spacing)
        {
            canvas.DrawRect(x, canvasArea.Top, 1.0f, canvasArea.Height, fillPaint);
        }

        for (float y = canvasArea.Top + spacing; y < canvasArea.Bottom; y += spacing)
        {
            canvas.DrawRect(canvasArea.Left, y, canvasArea.Width, 1.0f, fillPaint);
        }
    }

    private void DrawEraserCursor(SKCanvas canvas)
    {
        strokePaint.Color = Cursor;
        strokePaint.StrokeWidth = 1.5f;
        canvas.DrawCircle(pointer, EraserRadius, strokePaint);
    }

    private void DrawToolbar(SKCanvas canvas)
    {
        fillPaint.Color = Panel;
        canvas.DrawRect(0.0f, 0.0f, stage.Width, ToolbarHeight, fillPaint);

        fillPaint.Color = Divider;
        canvas.DrawRect(0.0f, ToolbarHeight - 1.0f, stage.Width, 1.0f, fillPaint);

        for (int index = 0; index < Palette.Length; index++)
        {
            SKRect swatch = swatchRects[index];

            fillPaint.Color = Palette[index];
            canvas.DrawRoundRect(swatch, 6.0f, 6.0f, fillPaint);

            if (index == colorIndex && !eraserMode)
            {
                strokePaint.Color = Highlight;
                strokePaint.StrokeWidth = 2.0f;
                canvas.DrawRoundRect(SKRect.Inflate(swatch, 4.0f, 4.0f), 9.0f, 9.0f, strokePaint);
            }
        }

        for (int index = 0; index < Widths.Length; index++)
        {
            SKRect slot = widthRects[index];

            fillPaint.Color = index == widthIndex ? Selected : Panel;
            canvas.DrawRoundRect(slot, 6.0f, 6.0f, fillPaint);

            fillPaint.Color = eraserMode ? Label : Palette[colorIndex];
            canvas.DrawCircle(slot.MidX, slot.MidY, Widths[index] * 0.5f, fillPaint);
        }

        DrawButton(canvas, eraserRect, "ERASE", eraserMode, true);
        DrawButton(canvas, clearRect, "CLEAR", false, strokes.Count > 0);
    }

    private void DrawButton(SKCanvas canvas, SKRect rect, string text, bool active, bool enabled)
    {
        SKColor accent = active ? Highlight : enabled ? Label : Divider;

        fillPaint.Color = active ? Selected : Panel;
        canvas.DrawRoundRect(rect, 6.0f, 6.0f, fillPaint);

        strokePaint.Color = accent;
        strokePaint.StrokeWidth = 1.5f;
        canvas.DrawRoundRect(rect, 6.0f, 6.0f, strokePaint);

        fillPaint.Color = accent;
        canvas.DrawText(text, rect.MidX, rect.MidY + 4.0f, SKTextAlign.Center, labelFont, fillPaint);
    }

    private void DrawStatus(SKCanvas canvas)
    {
        float top = stage.Height - StatusHeight;

        fillPaint.Color = Panel;
        canvas.DrawRect(0.0f, top, stage.Width, StatusHeight, fillPaint);

        fillPaint.Color = Divider;
        canvas.DrawRect(0.0f, top, stage.Width, 1.0f, fillPaint);

        int points = 0;

        foreach (Stroke stroke in strokes)
        {
            points += stroke.PointCount;
        }

        float baseline = top + (StatusHeight * 0.5f) + 4.0f;

        fillPaint.Color = Label;
        canvas.DrawText($"STROKES {strokes.Count}   POINTS {points}", SwatchGap, baseline, SKTextAlign.Left, labelFont, fillPaint);
        canvas.DrawText("DRAG TO DRAW   RIGHT DRAG TO ERASE", stage.Width - SwatchGap, baseline, SKTextAlign.Right, labelFont, fillPaint);
    }

    private void HandleToolbarClick(SKPoint position)
    {
        for (int index = 0; index < Palette.Length; index++)
        {
            if (swatchRects[index].Contains(position.X, position.Y))
            {
                SelectColor(index);

                return;
            }
        }

        for (int index = 0; index < Widths.Length; index++)
        {
            if (widthRects[index].Contains(position.X, position.Y))
            {
                SelectWidth(index);

                return;
            }
        }

        if (eraserRect.Contains(position.X, position.Y))
        {
            ToggleEraser();
        }
        else if (clearRect.Contains(position.X, position.Y))
        {
            Clear();
        }
    }

    private void Erase(SKPoint from, SKPoint to)
    {
        for (int index = strokes.Count - 1; index >= 0; index--)
        {
            if (strokes[index].Split(from, to, EraserRadius) is not { } fragments)
            {
                continue;
            }

            strokes[index].Dispose();
            strokes.RemoveAt(index);
            strokes.InsertRange(index, fragments);
            bakeDirty = true;
        }
    }
}