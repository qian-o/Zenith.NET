using System.Numerics;
using SkiaSharp;
using Zenith.NET;

namespace SkiaGallery;

internal sealed class Gallery : IDisposable
{
    private const float ExpandedSidebarWidth = 228.0f;
    private const float CompactSidebarWidth = 80.0f;
    private const float CompactBreakpoint = 1152.0f;
    private const float ContentRight = 32.0f;
    private const float DefaultContentTop = 142.0f;
    private const float DenseContentTop = 112.0f;
    private const float DefaultContentBottom = 82.0f;
    private const float ReducedContentBottom = 24.0f;
    private const float DefaultNavigationStep = 60.0f;
    private const float MinimumNavigationStep = 48.0f;
    private const float DenseHeightBreakpoint = 680.0f;
    private const double TransitionDuration = 0.22;

    private readonly GraphicsApi graphicsApi;
    private readonly string deviceName;
    private readonly GalleryResources resources = new();
    private readonly GalleryScene[] scenes;
    private readonly SKTextBlob[] titleBlobs;
    private readonly SKTextBlob[] descriptionBlobs;
    private readonly string[] descriptionTexts;
    private readonly SKTextBlob[] compactTitleBlobs;
    private readonly SKTextBlob pausedHeaderBlob;
    private readonly SKTextBlob pausedTitleBlob;
    private readonly SKTextBlob pausedDescriptionBlob;
    private readonly SKPaint activePaint = new() { Color = GalleryPalette.Accent, IsAntialias = true };
    private readonly SKPaint hoverPaint = new() { Color = new(49, 72, 64), IsAntialias = true };
    private readonly SKPaint titlePaint = new() { Color = GalleryPalette.Ink, IsAntialias = true };
    private readonly SKPaint descriptionPaint = new() { Color = GalleryPalette.Muted, IsAntialias = true };
    private readonly SKPaint pausedIconPaint = new() { Color = GalleryPalette.Accent, IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 2.0f, StrokeCap = SKStrokeCap.Round };
    private readonly float pausedHeaderWidth;
    private readonly float pausedTitleWidth;
    private readonly float pausedDescriptionWidth;

    private int activeIndex;
    private int hoverIndex = -1;
    private SKPicture? chromePicture;
    private SKPicture? navigationPicture;
    private float viewportWidth = -1.0f;
    private float viewportHeight = -1.0f;
    private float sidebarWidth;
    private float contentLeft;
    private float sceneWidth;
    private float sceneHeight;
    private float navigationTop;
    private float navigationStep = DefaultNavigationStep;
    private float contentTop;
    private float contentBottom;
    private bool compact;
    private bool dense;
    private bool layoutPaused;
    private bool showFooter = true;
    private bool showNavigation = true;
    private double lastSeconds;
    private double transitionSeconds;
    private bool dirty = true;
    private bool transitionCompleteRendered;

    public Gallery(GraphicsApi graphicsApi, string deviceName)
    {
        this.graphicsApi = graphicsApi;
        this.deviceName = deviceName;

        scenes =
        [
            new OverviewScene(resources),
            new GeometryScene(resources),
            new TypographyScene(resources),
            new PaintScene(resources),
            new MotionScene(resources)
        ];

        titleBlobs = new SKTextBlob[scenes.Length];
        descriptionBlobs = new SKTextBlob[scenes.Length];
        descriptionTexts = new string[scenes.Length];
        compactTitleBlobs = new SKTextBlob[scenes.Length];

        for (int i = 0; i < scenes.Length; i++)
        {
            titleBlobs[i] = resources.CreateText(scenes[i].Title, resources.TitleFont);
            descriptionTexts[i] = scenes[i].Description;
            descriptionBlobs[i] = resources.CreateText(descriptionTexts[i], resources.BodyFont);
            compactTitleBlobs[i] = resources.CreateText(scenes[i].Navigation, resources.SectionFont);
        }

        const string pausedHeader = "GPU scene paused at this window size";
        const string pausedTitle = "More room needed";
        const string pausedDescription = "Increase the window size to continue.";

        pausedHeaderBlob = resources.CreateText(pausedHeader, resources.BodyFont);
        pausedTitleBlob = resources.CreateText(pausedTitle, resources.SectionFont);
        pausedDescriptionBlob = resources.CreateText(pausedDescription, resources.BodyFont);
        pausedHeaderWidth = resources.BodyFont.MeasureText(pausedHeader);
        pausedTitleWidth = resources.SectionFont.MeasureText(pausedTitle);
        pausedDescriptionWidth = resources.BodyFont.MeasureText(pausedDescription);
    }

    public int SceneCount => scenes.Length;

    public bool ShouldRender(double seconds)
    {
        lastSeconds = seconds;

        return dirty || (!layoutPaused && scenes[activeIndex].IsAnimated) || !transitionCompleteRendered;
    }

    public void Draw(SKCanvas canvas, float width, float height, double seconds)
    {
        SetViewport(width, height);
        lastSeconds = seconds;
        dirty = false;

        float titleBaseline = dense ? 55.0f : 73.0f;
        float descriptionBaseline = dense ? 82.0f : 108.0f;
        float headerBottom = contentTop - 16.0f;

        canvas.Clear(GalleryPalette.Background);
        canvas.DrawPicture(chromePicture!);

        if (showNavigation && hoverIndex >= 0 && hoverIndex != activeIndex)
        {
            canvas.DrawRoundRect(NavigationRect(hoverIndex), 6.0f, 6.0f, hoverPaint);
        }

        if (showNavigation)
        {
            canvas.DrawRoundRect(NavigationRect(activeIndex), 6.0f, 6.0f, activePaint);
        }

        canvas.DrawPicture(navigationPicture!);

        canvas.Save();
        canvas.ClipRect(new(contentLeft, 0.0f, MathF.Max(contentLeft, width - ContentRight), headerBottom));

        if (layoutPaused)
        {
            canvas.DrawText(compactTitleBlobs[activeIndex], contentLeft, titleBaseline, titlePaint);

            if (sceneWidth >= pausedHeaderWidth)
            {
                canvas.DrawText(pausedHeaderBlob, contentLeft, descriptionBaseline, descriptionPaint);
            }
        }
        else
        {
            canvas.DrawText(titleBlobs[activeIndex], contentLeft, titleBaseline, titlePaint);
            canvas.DrawText(descriptionBlobs[activeIndex], contentLeft + 1.0f, descriptionBaseline, descriptionPaint);
        }

        canvas.Restore();

        if (layoutPaused)
        {
            transitionCompleteRendered = true;
            DrawPausedState(canvas, width, height);
            return;
        }

        float transition = Math.Clamp((float)((seconds - transitionSeconds) / TransitionDuration), 0.0f, 1.0f);
        float eased = 1.0f - MathF.Pow(1.0f - transition, 3.0f);

        transitionCompleteRendered = transition >= 1.0f;

        canvas.Save();
        canvas.ClipRect(new(contentLeft, contentTop, MathF.Max(contentLeft, width - ContentRight), MathF.Max(contentTop, height - contentBottom)));
        canvas.Translate(contentLeft + ((1.0f - eased) * 18.0f), contentTop);
        scenes[activeIndex].Draw(canvas, sceneWidth, sceneHeight, seconds);
        canvas.Restore();
    }

    public void PointerMove(Vector2 position)
    {
        int index = HitTest(position);

        if (index != hoverIndex)
        {
            hoverIndex = index;
            dirty = true;
        }
    }

    public void PointerDown(Vector2 position)
    {
        int index = HitTest(position);

        if (index >= 0)
        {
            Select(index);
        }
    }

    public void Previous()
    {
        Select((activeIndex + scenes.Length - 1) % scenes.Length);
    }

    public void Next()
    {
        Select((activeIndex + 1) % scenes.Length);
    }

    public void Select(int index)
    {
        if ((uint)index >= (uint)scenes.Length || index == activeIndex)
        {
            return;
        }

        activeIndex = index;
        transitionSeconds = lastSeconds;
        transitionCompleteRendered = false;
        dirty = true;
    }

    public void Dispose()
    {
        for (int i = scenes.Length - 1; i >= 0; i--)
        {
            scenes[i].Dispose();
            compactTitleBlobs[i].Dispose();
            descriptionBlobs[i].Dispose();
            titleBlobs[i].Dispose();
        }

        pausedDescriptionBlob.Dispose();
        pausedTitleBlob.Dispose();
        pausedHeaderBlob.Dispose();
        navigationPicture?.Dispose();
        chromePicture?.Dispose();
        pausedIconPaint.Dispose();
        descriptionPaint.Dispose();
        titlePaint.Dispose();
        hoverPaint.Dispose();
        activePaint.Dispose();
        resources.Dispose();
    }

    private SKPicture RecordChrome(float width, float height)
    {
        using SKPictureRecorder recorder = new();
        SKCanvas canvas = recorder.BeginRecording(new(0.0f, 0.0f, width, height));
        using SKPaint paint = new() { IsAntialias = true };

        paint.Color = GalleryPalette.Background;
        canvas.DrawRect(0.0f, 0.0f, width, height, paint);

        paint.Color = GalleryPalette.Navigation;
        canvas.DrawRect(0.0f, 0.0f, sidebarWidth, height, paint);

        paint.Color = GalleryPalette.Accent;
        float logoLeft = compact ? 20.0f : 28.0f;
        canvas.DrawRoundRect(new(logoLeft, 32.0f, logoLeft + 40.0f, 72.0f), 6.0f, 6.0f, paint);

        paint.Color = GalleryPalette.Navigation;
        canvas.DrawCircle(logoLeft + 20.0f, 52.0f, 7.0f, paint);

        if (!compact)
        {
            paint.Color = SKColors.White;
            canvas.DrawText("SKIA", 82.0f, 50.0f, SKTextAlign.Left, resources.NavigationFont, paint);

            paint.Color = new(137, 158, 150);
            canvas.DrawText("GPU GALLERY", 82.0f, 68.0f, SKTextAlign.Left, resources.CaptionFont, paint);
        }

        paint.Color = new(72, 96, 87);
        canvas.DrawLine(compact ? 16.0f : 24.0f, 118.0f, compact ? 64.0f : 204.0f, 118.0f, paint);

        float contentRight = MathF.Max(contentLeft, width - ContentRight);

        paint.Color = GalleryPalette.Line;
        canvas.DrawLine(contentLeft, contentTop - 16.0f, contentRight, contentTop - 16.0f, paint);

        if (showFooter)
        {
            float footerLine = height - 56.0f;
            canvas.DrawLine(contentLeft, footerLine, contentRight, footerLine, paint);

            paint.Color = GalleryPalette.Muted;
            float footerBaseline = height - 25.0f;
            string surfaceLabel = $"{graphicsApi}  /  SKIA GPU SURFACE";
            canvas.DrawText(surfaceLabel, contentLeft, footerBaseline, SKTextAlign.Left, resources.CaptionFont, paint);

            float labelRight = contentLeft + resources.CaptionFont.MeasureText(surfaceLabel);
            float deviceRight = contentRight;
            float deviceWidth = MathF.Max(0.0f, deviceRight - labelRight - 32.0f);

            if (deviceWidth > resources.CaptionFont.MeasureText("..."))
            {
                string displayDevice = FitText(deviceName, resources.CaptionFont, MathF.Min(430.0f, deviceWidth));
                canvas.DrawText(displayDevice, deviceRight, footerBaseline, SKTextAlign.Right, resources.CaptionFont, paint);
            }
        }

        return recorder.EndRecording();
    }

    private static string FitText(string text, SKFont font, float width)
    {
        if (font.MeasureText(text) <= width)
        {
            return text;
        }

        const string ellipsis = "...";
        int minimum = 0;
        int maximum = text.Length;

        while (minimum < maximum)
        {
            int length = (minimum + maximum + 1) / 2;
            string candidate = string.Concat(text.AsSpan(0, length), ellipsis);

            if (font.MeasureText(candidate) <= width)
            {
                minimum = length;
            }
            else
            {
                maximum = length - 1;
            }
        }

        return string.Concat(text.AsSpan(0, minimum), ellipsis);
    }

    private SKPicture RecordNavigation(float width, float height)
    {
        using SKPictureRecorder recorder = new();
        SKCanvas canvas = recorder.BeginRecording(new(0.0f, 0.0f, width, height));
        using SKPaint paint = new() { Color = SKColors.White, IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1.8f };

        if (!showNavigation)
        {
            return recorder.EndRecording();
        }

        for (int i = 0; i < scenes.Length; i++)
        {
            float centerX = compact ? 40.0f : 44.0f;
            float centerY = navigationTop + (i * navigationStep) + 24.0f;

            DrawNavigationIcon(canvas, paint, i, centerX, centerY);

            if (!compact)
            {
                paint.Style = SKPaintStyle.Fill;
                canvas.DrawText(scenes[i].Navigation, 70.0f, centerY + 5.0f, SKTextAlign.Left, resources.NavigationFont, paint);
            }
        }

        return recorder.EndRecording();
    }

    private static void DrawNavigationIcon(SKCanvas canvas, SKPaint paint, int index, float x, float y)
    {
        paint.Style = SKPaintStyle.Stroke;
        paint.Color = SKColors.White;

        if (index is 0)
        {
            canvas.DrawRoundRect(new(x - 8.0f, y - 8.0f, x - 1.0f, y - 1.0f), 1.5f, 1.5f, paint);
            canvas.DrawRoundRect(new(x + 2.0f, y - 8.0f, x + 8.0f, y + 8.0f), 1.5f, 1.5f, paint);
            canvas.DrawRoundRect(new(x - 8.0f, y + 2.0f, x - 1.0f, y + 8.0f), 1.5f, 1.5f, paint);
        }
        else if (index is 1)
        {
            canvas.DrawCircle(x - 3.0f, y - 2.0f, 6.0f, paint);
            canvas.DrawRect(new(x, y - 5.0f, x + 9.0f, y + 7.0f), paint);
        }
        else if (index is 2)
        {
            canvas.DrawLine(x - 8.0f, y + 8.0f, x, y - 8.0f, paint);
            canvas.DrawLine(x, y - 8.0f, x + 8.0f, y + 8.0f, paint);
            canvas.DrawLine(x - 4.0f, y + 1.0f, x + 4.0f, y + 1.0f, paint);
        }
        else if (index is 3)
        {
            canvas.DrawCircle(x - 4.0f, y - 3.0f, 5.0f, paint);
            canvas.DrawCircle(x + 4.0f, y - 3.0f, 5.0f, paint);
            canvas.DrawCircle(x, y + 4.0f, 5.0f, paint);
        }
        else
        {
            canvas.DrawCircle(x, y, 8.0f, paint);
            canvas.DrawCircle(x, y, 2.0f, paint);
            canvas.DrawLine(x - 11.0f, y, x - 7.0f, y, paint);
            canvas.DrawLine(x + 7.0f, y, x + 11.0f, y, paint);
        }
    }

    private void SetViewport(float width, float height)
    {
        bool nextCompact = width < CompactBreakpoint;
        bool nextDense = height < DenseHeightBreakpoint;
        bool nextShowFooter = !nextDense;
        float nextContentTop = nextDense ? DenseContentTop : DefaultContentTop;
        float nextContentBottom = nextShowFooter ? DefaultContentBottom : ReducedContentBottom;
        float nextSidebarWidth = nextCompact ? CompactSidebarWidth : ExpandedSidebarWidth;
        float nextContentLeft = nextSidebarWidth + (nextCompact ? 24.0f : 32.0f);
        float nextSceneWidth = MathF.Max(1.0f, width - nextContentLeft - ContentRight);
        float nextSceneHeight = MathF.Max(1.0f, height - nextContentTop - nextContentBottom);
        bool nextLayoutPaused = !scenes[activeIndex].CanRender(nextSceneWidth, nextSceneHeight);

        float nextNavigationTop = nextCompact ? 136.0f : 156.0f;
        float nextNavigationStep = DefaultNavigationStep;
        bool nextShowNavigation = true;

        if (nextDense)
        {
            nextNavigationTop = 104.0f;
            float availableStep = (height - nextNavigationTop - 56.0f) / (scenes.Length - 1.0f);
            nextShowNavigation = availableStep >= MinimumNavigationStep;
            nextNavigationStep = MathF.Min(DefaultNavigationStep, availableStep);
        }

        if (width != viewportWidth || height != viewportHeight || nextCompact != compact || nextDense != dense || nextLayoutPaused != layoutPaused || nextShowFooter != showFooter || nextShowNavigation != showNavigation)
        {
            if (nextSceneWidth != sceneWidth)
            {
                UpdateDescriptionBlobs(nextSceneWidth);
            }

            compact = nextCompact;
            dense = nextDense;
            layoutPaused = nextLayoutPaused;
            showFooter = nextShowFooter;
            showNavigation = nextShowNavigation;
            sidebarWidth = nextSidebarWidth;
            contentLeft = nextContentLeft;
            contentTop = nextContentTop;
            contentBottom = nextContentBottom;
            navigationTop = nextNavigationTop;
            navigationStep = nextNavigationStep;
            sceneWidth = nextSceneWidth;
            sceneHeight = nextSceneHeight;

            chromePicture?.Dispose();
            navigationPicture?.Dispose();
            chromePicture = RecordChrome(width, height);
            navigationPicture = RecordNavigation(width, height);
            viewportWidth = width;
            viewportHeight = height;
            dirty = true;
        }
    }

    private void UpdateDescriptionBlobs(float width)
    {
        for (int i = 0; i < scenes.Length; i++)
        {
            string text = FitText(scenes[i].Description, resources.BodyFont, width - 2.0f);

            if (text == descriptionTexts[i])
            {
                continue;
            }

            descriptionTexts[i] = text;
            descriptionBlobs[i].Dispose();
            descriptionBlobs[i] = resources.CreateText(text, resources.BodyFont);
        }
    }

    private int HitTest(Vector2 position)
    {
        if (!showNavigation)
        {
            return -1;
        }

        for (int i = 0; i < scenes.Length; i++)
        {
            if (NavigationRect(i).Contains(position.X, position.Y))
            {
                return i;
            }
        }

        return -1;
    }

    private SKRect NavigationRect(int index)
    {
        float top = navigationTop + (index * navigationStep);

        return compact ? new(10.0f, top, 70.0f, top + 48.0f) : new(18.0f, top, 210.0f, top + 48.0f);
    }

    private void DrawPausedState(SKCanvas canvas, float width, float height)
    {
        SKRect area = new(contentLeft, contentTop, MathF.Max(contentLeft, width - ContentRight), MathF.Max(contentTop, height - contentBottom));

        if (area.Width < 120.0f || area.Height < 54.0f)
        {
            return;
        }

        canvas.Save();
        canvas.ClipRect(area);

        float centerX = area.MidX;
        float titleBaseline = area.MidY + 11.0f;

        if (area.Height >= 130.0f)
        {
            SKRect icon = new(centerX - 20.0f, titleBaseline - 73.0f, centerX + 20.0f, titleBaseline - 43.0f);
            canvas.DrawRoundRect(icon, 4.0f, 4.0f, pausedIconPaint);
            canvas.DrawLine(icon.Left - 7.0f, icon.Top + 7.0f, icon.Left + 3.0f, icon.Top + 7.0f, pausedIconPaint);
            canvas.DrawLine(icon.Left + 7.0f, icon.Top - 7.0f, icon.Left + 7.0f, icon.Top + 3.0f, pausedIconPaint);
            canvas.DrawLine(icon.Right - 3.0f, icon.Bottom - 7.0f, icon.Right + 7.0f, icon.Bottom - 7.0f, pausedIconPaint);
            canvas.DrawLine(icon.Right - 7.0f, icon.Bottom - 3.0f, icon.Right - 7.0f, icon.Bottom + 7.0f, pausedIconPaint);
        }

        if (area.Width >= pausedTitleWidth + 24.0f)
        {
            canvas.DrawText(pausedTitleBlob, centerX - (pausedTitleWidth * 0.5f), titleBaseline, titlePaint);
        }

        if (area.Width >= pausedDescriptionWidth + 24.0f && area.Height >= 105.0f)
        {
            canvas.DrawText(pausedDescriptionBlob, centerX - (pausedDescriptionWidth * 0.5f), titleBaseline + 31.0f, descriptionPaint);
        }

        canvas.Restore();
    }
}