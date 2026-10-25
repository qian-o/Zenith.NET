using System.Numerics;
using SkiaSharp;
using Zenith.NET;
using Zenith.NET.Extensions.Skia;

namespace InkCanvas.Drawing;

internal class CanvasPresenter : DisposableObject
{
    private readonly Canvas canvas;
    private readonly SwapChain swapChain;
    private readonly Action<SKCanvas> draw;

    private SkiaTexture texture = null!;
    private Vector2 dpiScale;

    public CanvasPresenter(GraphicsContext context, Surface surface, Canvas canvas)
    {
        this.canvas = canvas;
        draw = Draw;
        swapChain = context.CreateSwapChain(new()
        {
            Surface = surface,
            Format = PixelFormat.B8G8R8A8UNorm
        });

        CreateTexture(surface.Width, surface.Height, canvas.IsMsaaEnabled);
    }

    public void Present(Vector2 dpiScale)
    {
        this.dpiScale = dpiScale;
        texture.Render(draw);

        Texture target = swapChain.Drawable;
        CommandBuffer commandBuffer = swapChain.Queue.CommandBuffer();

        commandBuffer.Transition(texture, default, texture.RequiredLayout, TextureLayout.CopySrc);
        commandBuffer.Transition(target, default, TextureLayout.Undefined, TextureLayout.CopyDst);
        commandBuffer.CopyTexture(texture, default, default, target, default, default, new()
        {
            Width = texture.Desc.Width,
            Height = texture.Desc.Height,
            Depth = 1
        });
        commandBuffer.Transition(target, default, TextureLayout.CopyDst, TextureLayout.Present);
        commandBuffer.Transition(texture, default, TextureLayout.CopySrc, texture.RequiredLayout);
        commandBuffer.Submit().Wait();

        swapChain.Present();
    }

    public void Resize(uint width, uint height)
    {
        if (texture.Desc.Width == width && texture.Desc.Height == height)
        {
            return;
        }

        bool isMultisamplingEnabled = texture.Desc.IsMultisamplingEnabled;

        DestroyTexture();
        swapChain.Resize(width, height);
        CreateTexture(width, height, isMultisamplingEnabled);
    }

    public void SetMultisamplingEnabled(bool isEnabled)
    {
        uint width = texture.Desc.Width;
        uint height = texture.Desc.Height;

        DestroyTexture();
        CreateTexture(width, height, isEnabled);
    }

    protected override void Destroy()
    {
        swapChain?.Queue.Timeline.Signal().Wait();
        DestroyTexture();
        swapChain?.Dispose();
    }

    private void CreateTexture(uint width, uint height, bool isMultisamplingEnabled)
    {
        texture = swapChain.Context.CreateSkiaTexture(new()
        {
            Format = swapChain.Desc.Format,
            Width = width,
            Height = height,
            IsMultisamplingEnabled = isMultisamplingEnabled
        });
    }

    private void DestroyTexture()
    {
        texture?.Dispose();
        texture = null!;
    }

    private void Draw(SKCanvas skiaCanvas)
    {
        skiaCanvas.Save();
        skiaCanvas.Scale(dpiScale.X, dpiScale.Y);

        canvas.Draw(skiaCanvas);

        skiaCanvas.Restore();
    }
}
