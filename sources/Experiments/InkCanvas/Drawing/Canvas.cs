using SkiaSharp;
using Zenith.NET;

namespace InkCanvas.Drawing;

internal class Canvas : DisposableObject
{
    private const float EraserRadius = 22.0f;

    private static readonly SKColor Surface = new(22, 24, 30);
    private static readonly SKColor Grid = new(32, 35, 43);
    private static readonly SKColor Cursor = new(150, 158, 172, 200);

    private readonly List<Stroke> strokes = [];
    private readonly Toolbar toolbar = new();
    private readonly SKPathBuilder eraserBuilder = new();
    private readonly SKPaint fillPaint = new();
    private readonly SKPaint strokePaint = new()
    {
        Style = SKPaintStyle.Stroke,
        StrokeCap = SKStrokeCap.Round,
        StrokeJoin = SKStrokeJoin.Round
    };

    private SKPicture? cachedPicture;
    private bool isPictureDirty = true;
    private Stroke? activeStroke;
    private SKPoint pointer;
    private bool isErasing;
    private bool isErasePending;
    private SKRect drawingArea;
    private SKSize size;

    public bool IsMsaaEnabled => toolbar.IsMsaaEnabled;

    private bool CanClear => strokes.Count > 0 || activeStroke is not null || isErasing || isErasePending;

    public void Resize(float width, float height)
    {
        if (width == size.Width && height == size.Height)
        {
            return;
        }

        size = new(width, height);
        drawingArea = new(0.0f, Toolbar.ToolbarHeight, width, MathF.Max(Toolbar.ToolbarHeight, height - Toolbar.StatusHeight));

        toolbar.Resize(width, height);
        isPictureDirty = true;
    }

    public void Draw(SKCanvas canvas)
    {
        if (isErasePending)
        {
            ApplyEraser();
        }

        fillPaint.Color = Surface;
        canvas.DrawRect(0.0f, 0.0f, size.Width, size.Height, fillPaint);

        if (isPictureDirty)
        {
            cachedPicture?.Dispose();
            cachedPicture = null;
            cachedPicture = RecordCanvas();
            isPictureDirty = false;
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

        if (isErasing && drawingArea.Contains(pointer.X, pointer.Y))
        {
            DrawEraserCursor(canvas);
        }

        canvas.Restore();

        toolbar.Draw(canvas, CanClear);
    }

    public void PointerMove(SKPoint position)
    {
        pointer = position;

        if (isErasing)
        {
            eraserBuilder.LineTo(position);
            isErasePending = true;
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

            return;
        }

        if (activeStroke is not null || isErasing)
        {
            return;
        }

        pointer = position;

        if (!erase && position.Y <= Toolbar.ToolbarHeight)
        {
            toolbar.SelectAt(position);

            return;
        }

        if (!drawingArea.Contains(position.X, position.Y))
        {
            return;
        }

        if (erase)
        {
            isErasing = true;
            eraserBuilder.MoveTo(position);
            eraserBuilder.LineTo(position);
            isErasePending = true;
        }
        else
        {
            activeStroke = new(toolbar.SelectedColor, toolbar.SelectedStrokeWidth);
            activeStroke.Add(position);
        }
    }

    public void PointerUp(bool erase)
    {
        if (erase && isErasing)
        {
            isErasing = false;

            if (isErasePending)
            {
                ApplyEraser();
            }
        }
        else if (!erase && activeStroke is not null)
        {
            activeStroke.Complete(strokePaint);
            strokes.Add(activeStroke);
            activeStroke = null;
            isPictureDirty = true;
        }
    }

    protected override void Destroy()
    {
        cachedPicture?.Dispose();
        activeStroke?.Dispose();

        if (strokes is not null)
        {
            foreach (Stroke stroke in strokes)
            {
                stroke.Dispose();
            }
            strokes.Clear();
        }

        strokePaint?.Dispose();
        fillPaint?.Dispose();
        eraserBuilder?.Dispose();
        toolbar?.Dispose();
    }

    private SKPicture RecordCanvas()
    {
        const float Spacing = 32.0f;

        using SKPictureRecorder recorder = new();
        SKCanvas canvas = recorder.BeginRecording(drawingArea);
        int nodeCount = 0;

        strokePaint.Color = Grid;
        strokePaint.StrokeWidth = 1.0f;

        for (float x = drawingArea.Left + Spacing; x < drawingArea.Right; x += Spacing)
        {
            canvas.DrawLine(x, drawingArea.Top, x, drawingArea.Bottom, strokePaint);
        }

        for (float y = drawingArea.Top + Spacing; y < drawingArea.Bottom; y += Spacing)
        {
            canvas.DrawLine(drawingArea.Left, y, drawingArea.Right, y, strokePaint);
        }

        foreach (Stroke stroke in strokes)
        {
            fillPaint.Color = stroke.Color;
            canvas.DrawPath(stroke.Path, fillPaint);
            nodeCount += stroke.NodeCount;
        }

        toolbar.SetStatistics(strokes.Count, nodeCount);

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
        if (!CanClear)
        {
            return;
        }

        foreach (Stroke stroke in strokes)
        {
            stroke.Dispose();
        }
        strokes.Clear();

        activeStroke?.Dispose();
        activeStroke = null;
        eraserBuilder.Reset();
        isErasing = false;
        isErasePending = false;
        isPictureDirty = true;
    }

    private void ApplyEraser()
    {
        using SKPath centerline = eraserBuilder.Detach();

        if (isErasing)
        {
            eraserBuilder.MoveTo(pointer);
        }

        strokePaint.StrokeWidth = EraserRadius * 2.0f;

        using SKPath eraser = strokePaint.GetFillPath(centerline)!;
        SKRect eraserBounds = eraser.Bounds;

        isErasePending = false;

        for (int i = strokes.Count - 1; i >= 0; i--)
        {
            Stroke stroke = strokes[i];

            if (!stroke.Erase(eraser, eraserBounds))
            {
                continue;
            }

            if (stroke.IsEmpty)
            {
                stroke.Dispose();
                strokes.RemoveAt(i);
            }

            isPictureDirty = true;
        }
    }
}
