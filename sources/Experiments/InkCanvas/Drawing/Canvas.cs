using SkiaSharp;

namespace InkCanvas.Drawing;

internal class Canvas : IDisposable
{
    private const float EraserRadius = 22.0f;

    private static readonly SKColor Surface = new(22, 24, 30);
    private static readonly SKColor Grid = new(32, 35, 43);
    private static readonly SKColor Cursor = new(150, 158, 172, 200);

    private readonly Toolbar toolbar = new();
    private readonly List<Stroke> strokes = [];

    private readonly SKPathBuilder eraserBuilder = new();
    private readonly SKPaint fillPaint = new() { IsAntialias = true };
    private readonly SKPaint strokePaint = new()
    {
        IsAntialias = true,
        Style = SKPaintStyle.Stroke,
        StrokeCap = SKStrokeCap.Round,
        StrokeJoin = SKStrokeJoin.Round
    };

    private SKPicture? cachedPicture;
    private int nodeCount;
    private bool pictureDirty = true;

    private Stroke? activeStroke;
    private SKPoint pointer;
    private bool erasing;
    private bool erasePending;

    private SKRect drawingArea;
    private SKSize size;

    public bool MSAA => toolbar.MSAA;

    private bool CanClear => strokes.Count > 0 || activeStroke is not null || erasing || erasePending;

    public void Draw(SKCanvas canvas, float width, float height)
    {
        EnsureLayout(width, height);

        if (erasePending)
        {
            ApplyEraser();
        }

        fillPaint.Color = Surface;
        canvas.DrawRect(0.0f, 0.0f, width, height, fillPaint);

        if (pictureDirty)
        {
            cachedPicture?.Dispose();
            cachedPicture = RecordCanvas();
            pictureDirty = false;
        }

        canvas.Save();
        canvas.ClipRect(drawingArea);
        canvas.DrawPicture(cachedPicture);

        if (activeStroke is not null)
        {
            strokePaint.Color = activeStroke.Color;
            strokePaint.StrokeWidth = activeStroke.Width;
            canvas.DrawPath(activeStroke.Path, strokePaint);
            canvas.DrawLine(activeStroke.TailStart, activeStroke.TailEnd, strokePaint);
        }

        if (erasing && drawingArea.Contains(pointer.X, pointer.Y))
        {
            DrawEraserCursor(canvas);
        }

        canvas.Restore();

        toolbar.Draw(canvas, strokes.Count, nodeCount, CanClear);
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
        if (!erase && toolbar.IsClearButton(position))
        {
            pointer = position;
            ClearCanvas();
        }
        else if (activeStroke is null && !erasing)
        {
            pointer = position;

            if (!erase && position.Y <= Toolbar.ToolbarHeight)
            {
                toolbar.SelectAt(position);
            }
            else if (drawingArea.Contains(position.X, position.Y))
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
                    activeStroke = new(toolbar.SelectedColor, toolbar.SelectedStrokeWidth);
                    activeStroke.Add(position);
                }
            }
        }
    }

    public void PointerUp(bool erase)
    {
        if (erase && erasing)
        {
            erasing = false;

            if (erasePending)
            {
                ApplyEraser();
            }
        }
        else if (!erase && activeStroke is not null)
        {
            activeStroke.Complete(strokePaint);
            strokes.Add(activeStroke);
            activeStroke = null;
            pictureDirty = true;
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
        cachedPicture?.Dispose();

        eraserBuilder.Dispose();
        strokePaint.Dispose();
        fillPaint.Dispose();

        toolbar.Dispose();
    }

    private void EnsureLayout(float width, float height)
    {
        if (width == size.Width && height == size.Height)
        {
            return;
        }

        size = new(width, height);
        drawingArea = new(0.0f, Toolbar.ToolbarHeight, width, MathF.Max(Toolbar.ToolbarHeight, height - Toolbar.StatusHeight));

        toolbar.Resize(width, height);
        pictureDirty = true;
    }

    private SKPicture RecordCanvas()
    {
        const float spacing = 32.0f;

        using SKPictureRecorder recorder = new();
        SKCanvas canvas = recorder.BeginRecording(drawingArea);
        int nodes = 0;

        strokePaint.Color = Grid;
        strokePaint.StrokeWidth = 1.0f;

        for (float x = drawingArea.Left + spacing; x < drawingArea.Right; x += spacing)
        {
            canvas.DrawLine(x, drawingArea.Top, x, drawingArea.Bottom, strokePaint);
        }

        for (float y = drawingArea.Top + spacing; y < drawingArea.Bottom; y += spacing)
        {
            canvas.DrawLine(drawingArea.Left, y, drawingArea.Right, y, strokePaint);
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
            pictureDirty = true;
        }
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

                pictureDirty = true;
            }
        }
    }
}
