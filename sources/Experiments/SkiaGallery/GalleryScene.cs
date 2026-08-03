using SkiaSharp;

namespace SkiaGallery;

internal abstract class GalleryScene(GalleryResources resources) : IDisposable
{
    private const float MinimumSceneWidth = 500.0f;
    private const float MinimumWideSceneHeight = 280.0f;
    private const float MinimumStackedSceneHeight = 420.0f;
    private const float WideLayoutMinimumWidth = 620.0f;
    private const float WideLayoutAspectRatio = 1.35f;

    private SKPicture? staticPicture;
    private float layoutWidth;
    private float layoutHeight;

    protected GalleryResources Resources { get; } = resources;

    public abstract string Navigation { get; }

    public abstract string Title { get; }

    public abstract string Description { get; }

    public virtual bool IsAnimated => false;

    public virtual bool CanRender(float width, float height)
    {
        return width >= MinimumSceneWidth && height >= (UseWideLayout(width, height) ? MinimumWideSceneHeight : MinimumStackedSceneHeight);
    }

    public void Draw(SKCanvas canvas, float width, float height, double seconds)
    {
        EnsureLayout(width, height);
        canvas.DrawPicture(staticPicture!);
        DrawDynamic(canvas, width, height, seconds);
    }

    public void Dispose()
    {
        staticPicture?.Dispose();
        DisposeResources();
    }

    protected abstract void UpdateLayout(float width, float height);

    protected abstract void DrawStatic(SKCanvas canvas, float width, float height);

    protected virtual void DrawDynamic(SKCanvas canvas, float width, float height, double seconds)
    {
    }

    protected virtual void DisposeResources()
    {
    }

    protected static bool UseWideLayout(float width, float height)
    {
        return width >= WideLayoutMinimumWidth && width >= height * WideLayoutAspectRatio;
    }

    private void EnsureLayout(float width, float height)
    {
        if (staticPicture is not null && layoutWidth == width && layoutHeight == height)
        {
            return;
        }

        staticPicture?.Dispose();
        layoutWidth = width;
        layoutHeight = height;
        UpdateLayout(width, height);

        using SKPictureRecorder recorder = new();
        SKCanvas canvas = recorder.BeginRecording(new(0.0f, 0.0f, width, height));
        DrawStatic(canvas, width, height);

        staticPicture = recorder.EndRecording();
    }
}