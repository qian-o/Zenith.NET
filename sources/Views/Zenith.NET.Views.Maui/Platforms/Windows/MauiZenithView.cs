using Microsoft.UI.Xaml.Controls;
using WinRT;

namespace Zenith.NET.Views.Maui.Platforms.Windows;

internal unsafe partial class MauiZenithView(ZenithViewHandler handler) : SwapChainPanel
{
    private D3DTexture? texture;
    private SwapChain? swapChain;

    public static Output Output { get; } = new()
    {
        ColorAttachments = [PixelFormat.B8G8R8A8UNorm],
        DepthStencilAttachment = PixelFormat.D24UNormS8UInt,
        SampleCount = SampleCount.Count1
    };

    public void UI(Action action)
    {
        if (DispatcherQueue.HasThreadAccess)
        {
            action();
        }
        else
        {
            bool isCompleted = false;

            DispatcherQueue.TryEnqueue(() =>
            {
                try
                {
                    action();
                }
                finally
                {
                    isCompleted = true;
                }
            });

            while (!isCompleted)
            {
                Thread.Yield();
            }
        }
    }

    public void EnsureResources()
    {
        if (handler.VirtualView.GraphicsContext is null)
        {
            return;
        }

        uint width = Math.Clamp((uint)Math.Ceiling(ActualWidth), 1, uint.MaxValue);
        uint height = Math.Clamp((uint)Math.Ceiling(ActualHeight), 1, uint.MaxValue);

        if (texture is null || texture.Width != width || texture.Height != height || swapChain is null)
        {
            ReleaseResources();

            texture = new(width, height);

            swapChain = handler.VirtualView.GraphicsContext.CreateSwapChain(new()
            {
                Surface = Surface.D3D11Interop(texture.SharedHandle, width, height),
                ColorTargetFormat = PixelFormat.B8G8R8A8UNorm,
                DepthStencilTargetFormat = PixelFormat.D24UNormS8UInt
            });

            this.As<ISwapChainPanelNative>().SetSwapChain(texture.SwapChain);
        }
    }

    public void Frame()
    {
        if (texture is null || swapChain is null)
        {
            return;
        }

        texture.AcquireSync();

        handler.VirtualView.OnUpdateRequested();
        handler.VirtualView.OnRenderRequested(swapChain.FrameBuffer);

        texture.ReleaseSync();
    }

    public void Present()
    {
        swapChain?.Present();
        texture?.Present();
    }

    public void ReleaseResources()
    {
        swapChain?.Dispose();
        swapChain = null;

        texture?.Dispose();
        texture = null;
    }
}
