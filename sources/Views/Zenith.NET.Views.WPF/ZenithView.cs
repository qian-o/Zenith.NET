using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;

namespace Zenith.NET.Views.WPF;

public class ZenithView : Control
{
    public static readonly DependencyProperty GraphicsContextProperty = DependencyProperty.Register(nameof(GraphicsContext),
                                                                                                    typeof(GraphicsContext),
                                                                                                    typeof(ZenithView),
                                                                                                    new PropertyMetadata(null, (d, _) => ((ZenithView)d).Initialize()));

    private readonly D3DImage image = new();

    private D3DTexture texture = new(100, 100);
    private SwapChain? swapChain;

    public ZenithView()
    {
        Initialize();
    }

    public GraphicsContext? GraphicsContext
    {
        get => (GraphicsContext?)GetValue(GraphicsContextProperty);
        set => SetValue(GraphicsContextProperty, value);
    }

    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
    {
        base.OnRenderSizeChanged(sizeInfo);

        if (ActualWidth <= 0 || ActualHeight <= 0)
        {
            return;
        }

        texture?.Dispose();
        texture = new((uint)ActualWidth, (uint)ActualHeight);

        image.Lock();
        image.SetBackBuffer(D3DResourceType.IDirect3DSurface9, texture.Handle);
        image.Unlock();

        Initialize();
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
    }

    private void Initialize()
    {
        swapChain?.Dispose();

        if (GraphicsContext is null)
        {
            return;
        }

        swapChain = GraphicsContext.CreateSwapChain(new()
        {
            Surface = Surface.D3D11Interop(texture.SharedHandle, texture.Width, texture.Height),
            ColorTargetFormat = PixelFormat.B8G8R8A8UNorm,
            DepthStencilTargetFormat = PixelFormat.D24UNormS8UInt
        });

        InvalidateVisual();
    }
}
