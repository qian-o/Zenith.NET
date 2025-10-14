using Android.Content;
using Android.Graphics;
using Android.Runtime;
using Android.Views;

namespace Zenith.NET.Views;

public class ZenithView(Context? context) : SurfaceView(context), ISurfaceHolderCallback
{
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
