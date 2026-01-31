using CoreAnimation;
using CoreGraphics;
using Foundation;
using ObjCRuntime;
using UIKit;

namespace Zenith.NET.Views.Maui.Platforms.iOS;

internal class MauiZenithView(ZenithViewHandler handler) : UIView
{
    private SwapChain? swapChain;

    public void EnsureResources()
    {
        CGSize size = Layer.PreferredFrameSize();

        if (handler.VirtualView.GraphicsContext is null || (uint)size.Height is 0 || (uint)size.Height is 0)
        {
            return;
        }

        uint width = (uint)Math.Clamp(size.Width, 1, uint.MaxValue);
        uint height = (uint)Math.Clamp(size.Height, 1, uint.MaxValue);

        if (swapChain is null)
        {
            swapChain = handler.VirtualView.GraphicsContext.CreateSwapChain(new()
            {
                Surface = Surface.Apple(Layer.Handle, width, height),
                ColorTargetFormat = ZenithViewHelper.ColorTargetFormat,
                DepthStencilTargetFormat = ZenithViewHelper.DepthStencilTargetFormat
            });
        }
        else if (swapChain.Desc.Surface.Width != width || swapChain.Desc.Surface.Height != height)
        {
            swapChain.Resize(width, height);
        }
    }

    public void Tick()
    {
        if (swapChain is null)
        {
            return;
        }

        handler.VirtualView.OnUpdateRequested();
        handler.VirtualView.OnRenderRequested(swapChain.FrameBuffer);
    }

    public void Present()
    {
        swapChain?.Present();
    }

    public void ReleaseResources()
    {
        swapChain?.Dispose();
        swapChain = null;
    }

    [Export("layerClass")]
    public static Class LayerClass()
    {
        return new Class(typeof(CAMetalLayer));
    }
}
