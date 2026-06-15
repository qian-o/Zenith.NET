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
        if (surface is null)
        {
            return;
        }

        surface.AcquireSync();

        handler.VirtualView.OnUpdateRequested();
        handler.VirtualView.OnRenderRequested(surface.Drawable);

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
