using Android.Graphics;
using Android.Views;

namespace Zenith.NET.Views.Maui.Platforms.Android;

internal class MauiZenithView(ZenithViewHandler handler) : SurfaceView(handler.Context), ISurfaceHolderCallback
{
    public ZenithView ZenithView => handler.VirtualView;

    public void Destroy()
    {
    }

    void ISurfaceHolderCallback.SurfaceChanged(ISurfaceHolder holder, Format format, int width, int height)
    {
        throw new NotImplementedException();
    }

    void ISurfaceHolderCallback.SurfaceCreated(ISurfaceHolder holder)
    {
        throw new NotImplementedException();
    }

    void ISurfaceHolderCallback.SurfaceDestroyed(ISurfaceHolder holder)
    {
        throw new NotImplementedException();
    }
}
