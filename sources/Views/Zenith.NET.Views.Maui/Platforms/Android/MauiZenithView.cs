using Android.Graphics;
using Android.Views;

namespace Zenith.NET.Views.Maui.Platforms.Android;

internal class MauiZenithView : SurfaceView, ISurfaceHolderCallback
{
    public MauiZenithView(ZenithViewHandler handler) : base(handler.Context)
    {
        ZenithView = handler.VirtualView;

        Holder?.AddCallback(this);

        SetWillNotDraw(false);
    }

    public ZenithView ZenithView { get; }

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
