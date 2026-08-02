using SkiaSharp;

namespace SkiaBoard;

internal class Board
{
    private const float ToolbarWidth = 112.0f;
    private const float MinBrushWidth = 1.0f;
    private const float MaxBrushWidth = 48.0f;
    private const float EraserWidthScale = 2.0f;

    private static readonly SKColor AccentColor = new(119, 190, 145);
    private static readonly SKColor BackgroundColor = new(219, 225, 221);
    private static readonly SKColor ToolbarColor = new(25, 42, 38);

    private static readonly SKColor PaperColor = new(250, 249, 246);

    private static readonly SKRect brushButton = new(12.0f, 16.0f, 52.0f, 66.0f);
    private static readonly SKRect eraserButton = new(60.0f, 16.0f, 100.0f, 66.0f);
    private static readonly SKRect undoButton = new(12.0f, 78.0f, 40.0f, 112.0f);
    private static readonly SKRect redoButton = new(42.0f, 78.0f, 70.0f, 112.0f);
    private static readonly SKRect clearButton = new(72.0f, 78.0f, 100.0f, 112.0f);

    private static readonly SKColor[] colors =
    [
        new(28, 34, 32),
        new(34, 101, 163),
        new(211, 63, 73),
        new(235, 151, 45),
        new(48, 139, 94),
        new(126, 79, 156)
    ];

    private readonly List<Stroke> strokes = [];
    private readonly Stack<Stroke[]> undoHistory = [];
    private readonly Stack<Stroke[]> redoHistory = [];

    private Stroke? activeStroke;
    private Tool tool;
    private int colorIndex;
    private SKPoint pointer;
    private bool adjustingBrush;
    private bool hasPointer;

    public float BrushWidth { get; private set; } = 8.0f;

    public string ToolName => tool is Tool.Brush ? "Brush" : "Eraser";

    private float StrokeWidth => tool is Tool.Eraser ? BrushWidth * EraserWidthScale : BrushWidth;

    public void Clear()
    {
        activeStroke = null;

        if (strokes.Count is 0)
        {
            return;
        }

        SaveState();
        strokes.Clear();
    }

    public void Draw(SKCanvas canvas, float width, float height)
    {
        canvas.Clear(BackgroundColor);

        SKRect paper = Paper(width, height);

        using SKPaint paint = new() { IsAntialias = true };

        paint.Color = new SKColor(108, 124, 115, 46);
        canvas.DrawRoundRect(new SKRect(paper.Left + 4.0f, paper.Top + 6.0f, paper.Right + 4.0f, paper.Bottom + 6.0f), 3.0f, 3.0f, paint);

        paint.Color = PaperColor;
        canvas.DrawRoundRect(paper, 3.0f, 3.0f, paint);

        DrawGrid(canvas, paper, paint);

        canvas.Save();
        canvas.ClipRect(paper);
        canvas.SaveLayer(paper, null);

        foreach (Stroke stroke in strokes)
        {
            DrawStroke(canvas, stroke, paint);
        }

        if (activeStroke is not null)
        {
            DrawStroke(canvas, activeStroke, paint);
        }

        canvas.Restore();
        canvas.Restore();

        DrawPointer(canvas, paper, paint);
        DrawToolbar(canvas, height, paint);
    }

    public void PointerDown(SKPoint point, float width, float height)
    {
        pointer = point;
        hasPointer = true;

        if (point.X < ToolbarWidth)
        {
            HandleToolbarClick(point, height);
            return;
        }

        if (!Paper(width, height).Contains(point.X, point.Y))
        {
            return;
        }

        activeStroke = new(colors[colorIndex], StrokeWidth, tool is Tool.Eraser);
        activeStroke.Points.Add(point);
    }

    public void PointerMove(SKPoint point, float width, float height)
    {
        pointer = point;
        hasPointer = true;

        if (adjustingBrush)
        {
            SetBrushWidth(point.Y, height);
            return;
        }

        if (activeStroke is null)
        {
            return;
        }

        SKRect paper = Paper(width, height);

        if (paper.Contains(point.X, point.Y))
        {
            AddPoint(activeStroke, point, false);
        }
        else
        {
            AddPoint(activeStroke, ClipToPaper(activeStroke.Points[^1], point, paper), true);
            CommitStroke();
        }
    }

    public void PointerUp(SKPoint point, float width, float height)
    {
        pointer = point;
        hasPointer = true;
        adjustingBrush = false;

        if (activeStroke is null)
        {
            return;
        }

        SKRect paper = Paper(width, height);
        AddPoint(activeStroke, paper.Contains(point.X, point.Y) ? point : ClipToPaper(activeStroke.Points[^1], point, paper), true);
        CommitStroke();
    }

    public void ResizeBrush(float delta)
    {
        BrushWidth = Math.Clamp(BrushWidth + delta, MinBrushWidth, MaxBrushWidth);
    }

    public void Undo()
    {
        activeStroke = null;

        if (undoHistory.TryPop(out Stroke[]? state))
        {
            redoHistory.Push([.. strokes]);
            Restore(state);
        }
    }

    public void Redo()
    {
        activeStroke = null;

        if (redoHistory.TryPop(out Stroke[]? state))
        {
            undoHistory.Push([.. strokes]);
            Restore(state);
        }
    }

    public void UseBrush()
    {
        tool = Tool.Brush;
    }

    public void UseEraser()
    {
        tool = Tool.Eraser;
    }

    private static void DrawGrid(SKCanvas canvas, SKRect paper, SKPaint paint)
    {
        paint.Color = new SKColor(92, 119, 105, 38);

        for (float y = paper.Top + 24.0f; y < paper.Bottom; y += 24.0f)
        {
            for (float x = paper.Left + 24.0f; x < paper.Right; x += 24.0f)
            {
                canvas.DrawCircle(x, y, 1.2f, paint);
            }
        }
    }

    private static void DrawStroke(SKCanvas canvas, Stroke stroke, SKPaint paint)
    {
        paint.Color = stroke.Color;
        paint.BlendMode = stroke.Eraser ? SKBlendMode.Clear : SKBlendMode.SrcOver;
        paint.Style = SKPaintStyle.Stroke;
        paint.StrokeCap = SKStrokeCap.Round;
        paint.StrokeJoin = SKStrokeJoin.Round;
        paint.StrokeWidth = stroke.Width;

        if (stroke.Points.Count is 1)
        {
            paint.Style = SKPaintStyle.Fill;
            canvas.DrawCircle(stroke.Points[0], stroke.Width * 0.5f, paint);
            return;
        }

        SKPathBuilder builder = new();
        builder.MoveTo(stroke.Points[0]);

        if (stroke.Points.Count is 2)
        {
            builder.LineTo(stroke.Points[1]);
        }
        else
        {
            for (int i = 1; i < stroke.Points.Count - 1; i++)
            {
                SKPoint point = stroke.Points[i];
                SKPoint next = stroke.Points[i + 1];
                builder.QuadTo(point, new SKPoint((point.X + next.X) * 0.5f, (point.Y + next.Y) * 0.5f));
            }

            builder.LineTo(stroke.Points[^1]);
        }

        using SKPath path = builder.Detach();
        canvas.DrawPath(path, paint);
    }

    private void DrawToolbar(SKCanvas canvas, float height, SKPaint paint)
    {
        paint.BlendMode = SKBlendMode.SrcOver;
        paint.Style = SKPaintStyle.Fill;
        paint.Color = ToolbarColor;
        canvas.DrawRect(0.0f, 0.0f, ToolbarWidth, height, paint);

        DrawToolButtons(canvas, paint);
        DrawDivider(canvas, paint, 126.0f);
        DrawPalette(canvas, paint);
        DrawDivider(canvas, paint, 278.0f);
        DrawBrushSlider(canvas, height, paint);
    }

    private void DrawToolButtons(SKCanvas canvas, SKPaint paint)
    {
        bool canUndo = undoHistory.Count > 0;
        bool canRedo = redoHistory.Count > 0;
        bool canClear = strokes.Count > 0;

        DrawButton(canvas, paint, brushButton, tool is Tool.Brush, true);
        DrawButton(canvas, paint, eraserButton, tool is Tool.Eraser, true);
        DrawButton(canvas, paint, undoButton, false, canUndo);
        DrawButton(canvas, paint, redoButton, false, canRedo);
        DrawButton(canvas, paint, clearButton, false, canClear);

        paint.Style = SKPaintStyle.Stroke;
        paint.StrokeWidth = 4.0f;
        paint.StrokeCap = SKStrokeCap.Round;
        paint.Color = tool is Tool.Brush ? ToolbarColor : SKColors.White;
        canvas.DrawLine(23.0f, 50.0f, 43.0f, 30.0f, paint);
        canvas.DrawCircle(21.0f, 52.0f, 3.0f, paint);

        paint.Color = tool is Tool.Eraser ? ToolbarColor : SKColors.White;
        paint.StrokeWidth = 2.5f;
        canvas.Save();
        canvas.RotateDegrees(-35.0f, 83.0f, 41.0f);
        canvas.DrawRoundRect(new SKRect(72.0f, 32.0f, 94.0f, 50.0f), 2.0f, 2.0f, paint);
        canvas.Restore();

        DrawUndoIcon(canvas, paint, undoButton, false, canUndo);
        DrawUndoIcon(canvas, paint, redoButton, true, canRedo);
        DrawClearIcon(canvas, paint, clearButton, canClear);
    }

    private void DrawPalette(SKCanvas canvas, SKPaint paint)
    {
        for (int i = 0; i < colors.Length; i++)
        {
            SKRect swatch = ColorButton(i);
            SKPoint center = new(swatch.MidX, swatch.MidY);
            bool hovered = HasPointer(swatch);

            paint.Style = SKPaintStyle.Fill;
            paint.Color = colors[i];
            canvas.DrawCircle(center, hovered ? 15.0f : 13.0f, paint);

            if (i == colorIndex)
            {
                paint.Style = SKPaintStyle.Stroke;
                paint.StrokeWidth = 2.5f;
                paint.Color = AccentColor;
                canvas.DrawCircle(center, 18.0f, paint);
            }
        }
    }

    private void DrawBrushSlider(SKCanvas canvas, float height, SKPaint paint)
    {
        SKRect track = BrushSlider(height);

        if (track.Height < 24.0f)
        {
            return;
        }

        float amount = (BrushWidth - MinBrushWidth) / (MaxBrushWidth - MinBrushWidth);
        float y = track.Bottom - (track.Height * amount);

        paint.Style = SKPaintStyle.Stroke;
        paint.StrokeCap = SKStrokeCap.Round;
        paint.StrokeWidth = 3.0f;
        paint.Color = new SKColor(93, 118, 108);
        canvas.DrawLine(track.MidX, track.Top, track.MidX, track.Bottom, paint);

        paint.Color = AccentColor;
        canvas.DrawLine(track.MidX, y, track.MidX, track.Bottom, paint);

        paint.Style = SKPaintStyle.Fill;
        paint.Color = HasPointer(new SKRect(track.Left - 10.0f, y - 12.0f, track.Right + 10.0f, y + 12.0f)) || adjustingBrush ? SKColors.White : AccentColor;
        canvas.DrawCircle(track.MidX, y, 8.0f, paint);

        float previewY = height - 42.0f;
        paint.Style = SKPaintStyle.Fill;
        paint.Color = tool is Tool.Eraser ? PaperColor : colors[colorIndex];
        canvas.DrawCircle(ToolbarWidth * 0.5f, previewY, MathF.Min(BrushWidth * 0.5f, 24.0f), paint);

        paint.Style = SKPaintStyle.Stroke;
        paint.StrokeWidth = 1.5f;
        paint.Color = new SKColor(210, 222, 216);
        canvas.DrawCircle(ToolbarWidth * 0.5f, previewY, MathF.Min(BrushWidth * 0.5f, 24.0f), paint);
    }

    private void DrawPointer(SKCanvas canvas, SKRect paper, SKPaint paint)
    {
        if (!hasPointer || !paper.Contains(pointer.X, pointer.Y))
        {
            return;
        }

        paint.BlendMode = SKBlendMode.SrcOver;
        paint.Style = SKPaintStyle.Stroke;
        paint.StrokeWidth = 1.5f;
        paint.Color = tool is Tool.Eraser ? new SKColor(36, 55, 49, 170) : colors[colorIndex];

        canvas.DrawCircle(pointer, StrokeWidth * 0.5f, paint);
    }

    private static void DrawUndoIcon(SKCanvas canvas, SKPaint paint, SKRect rect, bool redo, bool enabled)
    {
        paint.Style = SKPaintStyle.Stroke;
        paint.StrokeCap = SKStrokeCap.Round;
        paint.StrokeJoin = SKStrokeJoin.Round;
        paint.StrokeWidth = 2.0f;
        paint.Color = enabled ? SKColors.White : new SKColor(92, 112, 104);

        canvas.Save();

        if (redo)
        {
            canvas.Translate(rect.MidX * 2.0f, 0.0f);
            canvas.Scale(-1.0f, 1.0f);
        }

        SKRect arc = new(rect.MidX - 7.0f, rect.MidY - 7.0f, rect.MidX + 7.0f, rect.MidY + 7.0f);
        canvas.DrawArc(arc, 205.0f, 265.0f, false, paint);
        canvas.DrawLine(rect.MidX - 8.0f, rect.MidY - 3.0f, rect.MidX - 8.0f, rect.MidY + 3.0f, paint);
        canvas.DrawLine(rect.MidX - 8.0f, rect.MidY - 3.0f, rect.MidX - 2.0f, rect.MidY - 3.0f, paint);

        canvas.Restore();
    }

    private static void DrawClearIcon(SKCanvas canvas, SKPaint paint, SKRect rect, bool enabled)
    {
        paint.Style = SKPaintStyle.Stroke;
        paint.StrokeCap = SKStrokeCap.Round;
        paint.StrokeJoin = SKStrokeJoin.Round;
        paint.StrokeWidth = 2.0f;
        paint.Color = enabled ? SKColors.White : new SKColor(92, 112, 104);

        float x = rect.MidX;
        float y = rect.MidY;
        canvas.DrawRoundRect(new SKRect(x - 6.0f, y - 5.0f, x + 6.0f, y + 8.0f), 1.5f, 1.5f, paint);
        canvas.DrawLine(x - 8.0f, y - 8.0f, x + 8.0f, y - 8.0f, paint);
        canvas.DrawLine(x - 3.0f, y - 11.0f, x + 3.0f, y - 11.0f, paint);
        canvas.DrawLine(x - 2.0f, y - 2.0f, x - 2.0f, y + 5.0f, paint);
        canvas.DrawLine(x + 2.0f, y - 2.0f, x + 2.0f, y + 5.0f, paint);
    }

    private static void DrawDivider(SKCanvas canvas, SKPaint paint, float y)
    {
        paint.Style = SKPaintStyle.Stroke;
        paint.StrokeWidth = 1.0f;
        paint.Color = new SKColor(72, 94, 86);
        canvas.DrawLine(16.0f, y, ToolbarWidth - 16.0f, y, paint);
    }

    private void DrawButton(SKCanvas canvas, SKPaint paint, SKRect rect, bool selected, bool enabled)
    {
        paint.Style = SKPaintStyle.Fill;
        paint.Color = selected ? AccentColor : HasPointer(rect) && enabled ? new SKColor(47, 70, 63) : ToolbarColor;
        canvas.DrawRoundRect(rect, 5.0f, 5.0f, paint);
    }

    private bool HasPointer(SKRect rect)
    {
        return hasPointer && rect.Contains(pointer.X, pointer.Y);
    }

    private static SKRect Paper(float width, float height)
    {
        return new(ToolbarWidth + 16.0f,
                   16.0f,
                   MathF.Max(ToolbarWidth + 16.0f, width - 16.0f),
                   MathF.Max(16.0f, height - 16.0f));
    }

    private void HandleToolbarClick(SKPoint point, float height)
    {
        if (brushButton.Contains(point.X, point.Y))
        {
            UseBrush();
            return;
        }

        if (eraserButton.Contains(point.X, point.Y))
        {
            UseEraser();
            return;
        }

        if (undoButton.Contains(point.X, point.Y))
        {
            Undo();
            return;
        }

        if (redoButton.Contains(point.X, point.Y))
        {
            Redo();
            return;
        }

        if (clearButton.Contains(point.X, point.Y))
        {
            Clear();
            return;
        }

        if (BrushSlider(height).Contains(point.X, point.Y))
        {
            adjustingBrush = true;
            SetBrushWidth(point.Y, height);
            return;
        }

        for (int i = 0; i < colors.Length; i++)
        {
            if (ColorButton(i).Contains(point.X, point.Y))
            {
                colorIndex = i;
                UseBrush();
                return;
            }
        }
    }

    private static SKRect ColorButton(int index)
    {
        int column = index % 2;
        int row = index / 2;
        float left = 16.0f + (column * 48.0f);
        float top = 142.0f + (row * 44.0f);
        return new(left, top, left + 32.0f, top + 32.0f);
    }

    private static SKRect BrushSlider(float height)
    {
        return new(36.0f, 304.0f, 76.0f, MathF.Max(304.0f, height - 84.0f));
    }

    private void SetBrushWidth(float y, float height)
    {
        SKRect track = BrushSlider(height);
        float amount = 1.0f - Math.Clamp((y - track.Top) / track.Height, 0.0f, 1.0f);
        BrushWidth = MathF.Round((MinBrushWidth + (amount * (MaxBrushWidth - MinBrushWidth))) * 2.0f) * 0.5f;
    }

    private enum Tool
    {
        Brush,
        Eraser
    }

    private static void AddPoint(Stroke stroke, SKPoint point, bool includeEndpoint)
    {
        SKPoint previous = stroke.Points[^1];
        float x = point.X - previous.X;
        float y = point.Y - previous.Y;
        float distance = MathF.Sqrt((x * x) + (y * y));
        float spacing = MathF.Max(0.75f, stroke.Width * 0.1f);

        for (float offset = spacing; offset < distance; offset += spacing)
        {
            float amount = offset / distance;
            stroke.Points.Add(new(previous.X + (x * amount), previous.Y + (y * amount)));
        }

        if (includeEndpoint && distance > 0.01f)
        {
            stroke.Points.Add(point);
        }
    }

    private static SKPoint ClipToPaper(SKPoint start, SKPoint end, SKRect paper)
    {
        float x = end.X - start.X;
        float y = end.Y - start.Y;
        float amount = 1.0f;

        if (x > 0.0f)
        {
            amount = MathF.Min(amount, (paper.Right - start.X) / x);
        }
        else if (x < 0.0f)
        {
            amount = MathF.Min(amount, (paper.Left - start.X) / x);
        }

        if (y > 0.0f)
        {
            amount = MathF.Min(amount, (paper.Bottom - start.Y) / y);
        }
        else if (y < 0.0f)
        {
            amount = MathF.Min(amount, (paper.Top - start.Y) / y);
        }

        amount = Math.Clamp(amount, 0.0f, 1.0f);
        return new(start.X + (x * amount), start.Y + (y * amount));
    }

    private void CommitStroke()
    {
        if (activeStroke is null)
        {
            return;
        }

        SaveState();
        strokes.Add(activeStroke);
        activeStroke = null;
    }

    private void SaveState()
    {
        undoHistory.Push([.. strokes]);
        redoHistory.Clear();
    }

    private void Restore(Stroke[] state)
    {
        strokes.Clear();
        strokes.AddRange(state);
    }

    private class Stroke(SKColor color, float width, bool eraser)
    {
        public SKColor Color { get; } = color;

        public bool Eraser { get; } = eraser;

        public List<SKPoint> Points { get; } = [];

        public float Width { get; } = width;
    }
}