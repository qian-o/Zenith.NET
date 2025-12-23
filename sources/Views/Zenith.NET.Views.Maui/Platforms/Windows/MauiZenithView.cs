using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using WinRT;

namespace Zenith.NET.Views.Maui.Platforms.Windows;

internal unsafe partial class MauiZenithView : SwapChainPanel
{
    private readonly ViewTimer timer = new();

    private D3DTexture? texture;
    private SwapChain? swapChain;

    public MauiZenithView(ZenithViewHandler handler)
    {
        Loaded += (_, _) =>
        {
            timer.Start();

            CompositionTarget.Rendering += OnRendering;
        };

        Unloaded += (_, _) =>
        {
            CompositionTarget.Rendering -= OnRendering;

            timer.Reset();

            Destroy();
        };

        ZenithView = handler.VirtualView;
    }

    public static Output Output { get; } = new()
    {
        ColorAttachments = [PixelFormat.B8G8R8A8UNorm],
        DepthStencilAttachment = PixelFormat.D24UNormS8UInt,
        SampleCount = SampleCount.Count1
    };

    public ZenithView ZenithView { get; }

    public void Destroy()
    {
        swapChain?.Dispose();
        swapChain = null;

        texture?.Dispose();
        texture = null;
    }

    private void OnRendering(object? sender, object e)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            if (ZenithView.GraphicsContext is null)
            {
                return;
            }

            uint width = Math.Clamp((uint)Math.Ceiling(ActualWidth), 1, uint.MaxValue);
            uint height = Math.Clamp((uint)Math.Ceiling(ActualHeight), 1, uint.MaxValue);

            if (texture is null || texture.Width != width || texture.Height != height || swapChain is null)
            {
                Destroy();

                texture = new(width, height);
                swapChain = ZenithView.GraphicsContext.CreateSwapChain(new()
                {
                    Surface = Surface.D3D11Interop(texture.SharedHandle, width, height),
                    ColorTargetFormat = PixelFormat.B8G8R8A8UNorm,
                    DepthStencilTargetFormat = PixelFormat.D24UNormS8UInt
                });

                this.As<ISwapChainPanelNative>().SetSwapChain(texture.SwapChain);
            }

            texture.AcquireForUpdate();

            ZenithView.OnUpdateRequested(new(timer.GetAndRestartUpdate(), timer.TotalSeconds));
            ZenithView.OnRenderRequested(new(timer.GetAndRestartRender(), timer.TotalSeconds, swapChain.FrameBuffer));

            swapChain.Present();

            texture.PresentAndRelease();
        });
    }
}
