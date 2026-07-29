using Microsoft.UI.Xaml.Controls;
using WinRT;

namespace Zenith.NET.Views.Maui.Platforms.Windows;

internal unsafe partial class MauiZenithView(ZenithViewHandler handler) : SwapChainPanel
{
    private Surface? surface;

    public void EnsureResources()
    {
        if (handler.VirtualView.GraphicsContext is null)
        {
            return;
        }

        uint width = Math.Clamp((uint)Math.Ceiling(ActualWidth), 1, uint.MaxValue);
        uint height = Math.Clamp((uint)Math.Ceiling(ActualHeight), 1, uint.MaxValue);

        if (surface is null || surface.Width != width || surface.Height != height)
        {
            ReleaseResources();

            surface = new(handler.VirtualView.GraphicsContext, width, height);

            this.As<ISwapChainPanelNative>().SetSwapChain(surface.SwapChain);
        }
    }

    public void Tick()
    {
        if (handler.VirtualView.GraphicsContext is null || surface is null)
        {
            return;
        }

        surface.AcquireSync();

        CommandBuffer commandBuffer = handler.VirtualView.GraphicsContext.GraphicsQueue.CommandBuffer();

        commandBuffer.Transition(surface.Drawable, default, TextureLayout.Undefined, TextureLayout.ColorAttachment);

        handler.VirtualView.OnUpdateRequested();
        handler.VirtualView.OnRenderRequested(commandBuffer, surface.Drawable);

        commandBuffer.Transition(surface.Drawable, default, TextureLayout.ColorAttachment, TextureLayout.Common);

        commandBuffer.Submit().Wait();

        surface.ReleaseSync();
    }

    public void Present()
    {
        surface?.Present();
    }

    public void ReleaseResources()
    {
        surface?.Dispose();
        surface = null;
    }
}
