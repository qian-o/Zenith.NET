#if WINDOWS
using WinRT;

namespace Zenith.NET.Views.WinUI;

public unsafe partial class ZenithView
{
    private D3DTexture? texture;
    private SwapChain? swapChain;

    private void OnRender(GraphicsContext context)
    {
        uint width = Math.Clamp((uint)Math.Ceiling(ActualWidth), 1, uint.MaxValue);
        uint height = Math.Clamp((uint)Math.Ceiling(ActualHeight), 1, uint.MaxValue);

        if (texture is null || texture.Width != width || texture.Height != height || swapChain is null)
        {
            Destroy();

            texture = new(width, height);
            swapChain = context.CreateSwapChain(new()
            {
                Surface = Surface.D3D11Interop(texture.SharedHandle, width, height),
                ColorTargetFormat = PixelFormat.B8G8R8A8UNorm,
                DepthStencilTargetFormat = PixelFormat.D24UNormS8UInt
            });

            this.As<ISwapChainPanelNative>().SetSwapChain(texture.SwapChain);
        }

        UpdateRequested?.Invoke(this, new(timer.GetAndRestartUpdate(), timer.TotalSeconds));
        RenderRequested?.Invoke(this, new(timer.GetAndRestartRender(), timer.TotalSeconds, swapChain.FrameBuffer));

        texture.Present();
        swapChain.Present();
    }

    private void Destroy()
    {
        swapChain?.Dispose();
        swapChain = null;

        texture?.Dispose();
        texture = null;
    }
}
#endif