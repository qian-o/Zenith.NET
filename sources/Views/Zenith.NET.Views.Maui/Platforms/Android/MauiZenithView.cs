using Android.Graphics;
using Android.Runtime;
using Android.Views;

namespace Zenith.NET.Views;

internal class MauiZenithView(ZenithViewHandler handler) : SurfaceView(handler.Context), ISurfaceHolderCallback
{
    public ZenithView ZenithView => handler.VirtualView;

    public void SurfaceChanged(ISurfaceHolder holder, [GeneratedEnum] Format format, int width, int height)
    {
        throw new NotImplementedException();
    }

    public void SurfaceCreated(ISurfaceHolder holder)
    {
        throw new NotImplementedException();
    }

    public void SurfaceDestroyed(ISurfaceHolder holder)
    {
        throw new NotImplementedException();
    }
}
