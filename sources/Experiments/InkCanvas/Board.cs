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
    private readonly SKPathBuilder eraserBuilder = new();
    private readonly SKFont labelFont;
    private readonly SKPaint fillPaint = new() { IsAntialias = true };
    private readonly SKPaint strokePaint = new()
    {
        IsAntialias = true,
        Style = SKPaintStyle.Stroke,
        StrokeCap = SKStrokeCap.Round,
        StrokeJoin = SKStrokeJoin.Round
    };

    private SKPicture? bakedCanvas;
    private Stroke? activeStroke;
    private SKRect canvasArea;
    private SKRect clearRect;
    private SKPoint pointer;
    private int colorIndex;
    private int widthIndex = 1;
    private int nodeCount;
    private bool erasing;
    private bool erasePending;
    private bool bakeDirty = true;
    private float layoutWidth = -1.0f;
    private float layoutHeight = -1.0f;

    private bool CanClear => strokes.Count > 0 || activeStroke is not null || erasing || erasePending;

    public Board()
    {
        string family = OperatingSystem.IsMacOS() ? "SF Pro Text" : OperatingSystem.IsWindows() ? "Segoe UI" : "Noto Sans";

        using SKTypeface typeface = SKTypeface.FromFamilyName(family, SKFontStyle.Normal);

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

        if (erasePending)
        {
            ApplyEraser();
        }

        fillPaint.Color = Surface;
        canvas.DrawRect(0.0f, 0.0f, width, height, fillPaint);

        if (bakeDirty)
        {
            bakedCanvas?.Dispose();
            bakedCanvas = BakeCanvas();
            bakeDirty = false;
        }

        canvas.Save();
        canvas.ClipRect(canvasArea);
        canvas.DrawPicture(bakedCanvas);

        if (activeStroke is not null)
        {
            strokePaint.Color = activeStroke.Color;
            strokePaint.StrokeWidth = activeStroke.Width;
            canvas.DrawPath(activeStroke.Path, strokePaint);
            canvas.DrawLine(activeStroke.TailStart, activeStroke.TailEnd, strokePaint);
        }

        if (erasing && canvasArea.Contains(pointer.X, pointer.Y))
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

        if (erasing)
        {
            eraserBuilder.LineTo(position);
            erasePending = true;
        }
        else
        {
            activeStroke?.Add(position);
        }
    }

    public void PointerDown(SKPoint position, bool erase)
    {
        if (!erase && clearRect.Contains(position.X, position.Y))
        {
            pointer = position;
            ClearCanvas();
        }
        else if (activeStroke is null && !erasing)
        {
            pointer = position;

            if (!erase && position.Y <= ToolbarHeight)
            {
                HandleToolbarClick(position);
            }
            else if (canvasArea.Contains(position.X, position.Y))
            {
                if (erase)
                {
                    erasing = true;
                    eraserBuilder.MoveTo(position);
                    eraserBuilder.LineTo(position);
                    erasePending = true;
                }
                else
                {
                    activeStroke = new(Palette[colorIndex], Widths[widthIndex]);
                    activeStroke.Add(position);
                }
            }
        }
    }

    public void PointerUp(bool erase)
    {
        if (erase)
        {
            if (erasing)
            {
                erasing = false;

                if (erasePending)
                {
                    ApplyEraser();
                }
            }
        }
        else if (activeStroke is not null)
        {
            activeStroke.Complete(strokePaint);
            strokes.Add(activeStroke);
            activeStroke = null;
            bakeDirty = true;
        }
    }

    public void Dispose()
    {
        foreach (Stroke stroke in strokes)
        {
            stroke.Dispose();
        }

        strokes.Clear();
        activeStroke?.Dispose();
        bakedCanvas?.Dispose();
        eraserBuilder.Dispose();
        strokePaint.Dispose();
        fillPaint.Dispose();
        labelFont.Dispose();
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

        float clearLeft = MathF.Max(widthRects[^1].Right + (SwatchGap * 2.0f), width - SwatchGap - ButtonWidth);
        clearRect = new(clearLeft, top, clearLeft + ButtonWidth, bottom);
        bakeDirty = true;
    }

    private SKPicture BakeCanvas()
    {
        const float spacing = 32.0f;

        using SKPictureRecorder recorder = new();
        SKCanvas canvas = recorder.BeginRecording(canvasArea);
        int nodes = 0;

        strokePaint.Color = Grid;
        strokePaint.StrokeWidth = 1.0f;

        for (float x = canvasArea.Left + spacing; x < canvasArea.Right; x += spacing)
        {
            canvas.DrawLine(x, canvasArea.Top, x, canvasArea.Bottom, strokePaint);
        }

        for (float y = canvasArea.Top + spacing; y < canvasArea.Bottom; y += spacing)
        {
            canvas.DrawLine(canvasArea.Left, y, canvasArea.Right, y, strokePaint);
        }

        foreach (Stroke stroke in strokes)
        {
            fillPaint.Color = stroke.Color;
            canvas.DrawPath(stroke.Path, fillPaint);
            nodes += stroke.NodeCount;
        }

        nodeCount = nodes;

        return recorder.EndRecording();
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
        canvas.DrawRect(0.0f, 0.0f, layoutWidth, ToolbarHeight, fillPaint);

        fillPaint.Color = Divider;
        canvas.DrawRect(0.0f, ToolbarHeight - 1.0f, layoutWidth, 1.0f, fillPaint);

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

        for (int index = 0; index < Widths.Length; index++)
        {
            SKRect slot = widthRects[index];

            fillPaint.Color = index == widthIndex ? Selected : Panel;
            canvas.DrawRoundRect(slot, 6.0f, 6.0f, fillPaint);

            fillPaint.Color = Palette[colorIndex];
            canvas.DrawCircle(slot.MidX, slot.MidY, Widths[index] * 0.5f, fillPaint);
        }

        SKColor accent = CanClear ? Label : Divider;

        fillPaint.Color = Panel;
        canvas.DrawRoundRect(clearRect, 6.0f, 6.0f, fillPaint);

        strokePaint.Color = accent;
        strokePaint.StrokeWidth = 1.5f;
        canvas.DrawRoundRect(clearRect, 6.0f, 6.0f, strokePaint);

        fillPaint.Color = accent;
        canvas.DrawText("CLEAR", clearRect.MidX, clearRect.MidY + 4.0f, SKTextAlign.Center, labelFont, fillPaint);
    }

    private void DrawStatus(SKCanvas canvas)
    {
        float top = layoutHeight - StatusHeight;

        fillPaint.Color = Panel;
        canvas.DrawRect(0.0f, top, layoutWidth, StatusHeight, fillPaint);

        fillPaint.Color = Divider;
        canvas.DrawRect(0.0f, top, layoutWidth, 1.0f, fillPaint);

        float baseline = top + (StatusHeight * 0.5f) + 4.0f;

        fillPaint.Color = Label;
        canvas.DrawText($"STROKES {strokes.Count}   NODES {nodeCount}", SwatchGap, baseline, SKTextAlign.Left, labelFont, fillPaint);
        canvas.DrawText("DRAG TO DRAW   RIGHT DRAG TO ERASE", layoutWidth - SwatchGap, baseline, SKTextAlign.Right, labelFont, fillPaint);
    }

    private void HandleToolbarClick(SKPoint position)
    {
        int swatch = IndexAt(swatchRects, position);
        int slot = IndexAt(widthRects, position);

        if (swatch >= 0)
        {
            colorIndex = swatch;
        }
        else if (slot >= 0)
        {
            widthIndex = slot;
        }
    }

    private void ClearCanvas()
    {
        if (CanClear)
        {
            foreach (Stroke stroke in strokes)
            {
                stroke.Dispose();
            }

            strokes.Clear();
            activeStroke?.Dispose();
            activeStroke = null;
            eraserBuilder.Reset();
            erasing = false;
            erasePending = false;
            bakeDirty = true;
        }
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

    private void ApplyEraser()
    {
        using SKPath centerline = eraserBuilder.Detach();

        if (erasing)
        {
            eraserBuilder.MoveTo(pointer);
        }

        strokePaint.StrokeWidth = EraserRadius * 2.0f;

        using SKPath eraser = strokePaint.GetFillPath(centerline)!;
        SKRect eraserBounds = eraser.Bounds;

        erasePending = false;

        for (int index = strokes.Count - 1; index >= 0; index--)
        {
            Stroke stroke = strokes[index];

            if (stroke.Erase(eraser, eraserBounds))
            {
                if (stroke.IsEmpty)
                {
                    stroke.Dispose();
                    strokes.RemoveAt(index);
                }

                bakeDirty = true;
            }
        }
    }
}