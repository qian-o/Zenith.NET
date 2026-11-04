using Zenith.NET;
using Zenith.NET.Extensions.Skia;

namespace InkCanvas.Drawing;

internal class CanvasPresenter : DisposableObject
{
    private readonly SwapChain swapChain;

    private SkiaTexture? texture;

    public CanvasPresenter(Surface surface)
    {
        swapChain = App.Context.CreateSwapChain(new()
        {
            Surface = surface,
            Format = PixelFormat.B8G8R8A8UNorm
        });
    }

    public void Present(Canvas canvas)
    {
        if (texture is null || texture.Desc.Width != App.PhysicalWidth || texture.Desc.Height != App.PhysicalHeight || texture.Desc.IsMultisamplingEnabled != canvas.IsMsaaEnabled)
        {
            texture?.Dispose();
            texture = null;

            texture = App.Context.CreateSkiaTexture(new()
            {
                Format = swapChain.Desc.Format,
                Width = App.PhysicalWidth,
                Height = App.PhysicalHeight,
                IsMultisamplingEnabled = canvas.IsMsaaEnabled
            });
        }

        texture.Render(canvas.Draw);

        CommandBuffer commandBuffer = swapChain.Queue.CommandBuffer();

        commandBuffer.Transition(texture, default, texture.RequiredLayout, TextureLayout.CopySrc);
        commandBuffer.Transition(swapChain.Drawable, default, TextureLayout.Undefined, TextureLayout.CopyDst);

        commandBuffer.CopyTexture(texture, default, default, swapChain.Drawable, default, default, new()
        {
            Width = App.PhysicalWidth,
            Height = App.PhysicalHeight,
            Depth = 1
        });

        commandBuffer.Transition(swapChain.Drawable, default, TextureLayout.CopyDst, TextureLayout.Present);
        commandBuffer.Transition(texture, default, TextureLayout.CopySrc, texture.RequiredLayout);
        commandBuffer.Submit().Wait();

        swapChain.Present();
    }

    public void Resize()
    {
        swapChain.Resize(App.PhysicalWidth, App.PhysicalHeight);

        texture?.Dispose();
        texture = null;
    }

    protected override void Destroy()
    {
        texture?.Dispose();
        swapChain.Dispose();

        texture = null;
    }
}
