using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;

namespace Zenith.NET.Views.WPF;

public class ZenithView : Control
{
    private readonly D3DImage image;

    private D3DTexture? texture;

    public ZenithView()
    {
        image = new();
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
    }
}
