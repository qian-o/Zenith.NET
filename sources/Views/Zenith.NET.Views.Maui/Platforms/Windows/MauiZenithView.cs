using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using WinRT;
using Color = Windows.UI.Color;
using Grid = Microsoft.UI.Xaml.Controls.Grid;
using LinearGradientBrush = Microsoft.UI.Xaml.Media.LinearGradientBrush;

namespace Zenith.NET.Views.Maui.Platforms.Windows;

internal unsafe partial class MauiZenithView : SwapChainPanel
{
    private readonly ViewTimer timer = new();
    private readonly Grid previewGrid;
    private readonly LinearGradientBrush previewBrush;
    private readonly TextBlock previewTextBlock;

    private D3DTexture? texture;
    private SwapChain? swapChain;

    public MauiZenithView(ZenithViewHandler handler)
    {
        Loaded += (_, _) => timer.Start();

        Unloaded += (_, _) =>
        {
            timer.Stop();

            Destroy();

            timer.Reset();
        };

        EffectiveViewportChanged += (_, e) =>
        {
            CompositionTarget.Rendering -= OnRendering;

            if (e.EffectiveViewport.Width is not 0 && e.EffectiveViewport.Height is not 0)
            {
                CompositionTarget.Rendering += OnRendering;
            }
        };

        Children.Add(previewGrid = new Grid()
        {
            Background = previewBrush = new LinearGradientBrush()
            {
                GradientStops =
                [
                    new() { Color = Color.FromArgb(0xFF, 0x51, 0x2B, 0xD4), Offset = 0.0 },
                    new() { Color = Color.FromArgb(0xFF, 0x8A, 0x58, 0xFF), Offset = 0.45 },
                    new() { Color = Color.FromArgb(0xFF, 0x00, 0xA4, 0xEF), Offset = 1.0 }
                ],
                StartPoint = new(0.0, 0.0),
                EndPoint = new(1.0, 1.0),
                SpreadMethod = GradientSpreadMethod.Reflect
            },
            IsHitTestVisible = false
        });

        previewGrid.Children.Add(previewTextBlock = new()
        {
            Text = "ZenithView (No GraphicsContext)",
            HorizontalAlignment = Microsoft.UI.Xaml.HorizontalAlignment.Center,
            VerticalAlignment = Microsoft.UI.Xaml.VerticalAlignment.Center
        });

        ZenithView = handler.VirtualView;
    }

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
        if (ZenithView.GraphicsContext is null)
        {
            previewGrid.Visibility = Microsoft.UI.Xaml.Visibility.Visible;

            previewBrush.RelativeTransform = new TranslateTransform()
            {
                X = timer.TotalSeconds * 0.06 % 1.0,
                Y = timer.TotalSeconds * 0.06 % 1.0
            };

            previewTextBlock.FontSize = Math.Clamp(ActualHeight / 15.0, 14.0, 48.0);
        }
        else
        {
            previewGrid.Visibility = Microsoft.UI.Xaml.Visibility.Collapsed;

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

            ZenithView.OnUpdateRequested(new(timer.GetAndRestartUpdate(), timer.TotalSeconds));
            ZenithView.OnRenderRequested(new(timer.GetAndRestartRender(), timer.TotalSeconds, swapChain.FrameBuffer));

            texture.Present();
            swapChain.Present();
        }
    }
}
