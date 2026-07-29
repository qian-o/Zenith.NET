using CoreAnimation;
using CoreGraphics;
using Foundation;
using ObjCRuntime;
using UIKit;

namespace Zenith.NET.Views.Maui.Platforms.MacCatalyst;

internal class MauiZenithView(ZenithViewHandler handler) : UIView
{
    private SwapChain? swapChain;

    public void EnsureResources()
    {
        CGSize size = Layer.PreferredFrameSize();

        if (handler.VirtualView.GraphicsContext is null || size.Width.Value is 0 || size.Height.Value is 0)
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
                Format = ZenithViewHelper.DrawableFormat
            });
        }
        else if (swapChain.Desc.Surface.Width != width || swapChain.Desc.Surface.Height != height)
        {
            swapChain.Resize(width, height);
        }
    }

    public void Tick()
    {
        if (handler.VirtualView.GraphicsContext is null || swapChain is null)
        {
            return;
        }

        CommandBuffer commandBuffer = handler.VirtualView.GraphicsContext.GraphicsQueue.CommandBuffer();

        commandBuffer.Transition(swapChain.Drawable, default, TextureLayout.Undefined, TextureLayout.ColorAttachment);

        handler.VirtualView.OnUpdateRequested();
        handler.VirtualView.OnRenderRequested(commandBuffer, swapChain.Drawable);

        commandBuffer.Transition(swapChain.Drawable, default, TextureLayout.ColorAttachment, TextureLayout.Present);

        commandBuffer.Submit().Wait();
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
        return new(typeof(CAMetalLayer));
    }
}
