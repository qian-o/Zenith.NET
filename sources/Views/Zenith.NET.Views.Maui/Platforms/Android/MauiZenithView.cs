using System.Runtime.InteropServices;
using Android.Views;
using Java.Interop;

namespace Zenith.NET.Views.Maui.Platforms.Android;

internal partial class MauiZenithView(ZenithViewHandler handler) : SurfaceView(handler.Context)
{
    [LibraryImport("android", EntryPoint = "ANativeWindow_fromSurface")]
    private static partial nint ANativeWindowFromSurface(nint env, nint surface);

    private SwapChain? swapChain;

    public void EnsureResources()
    {
        if (!ValidateSurface() || handler.VirtualView.GraphicsContext is null || Width is 0 || Height is 0)
        {
            return;
        }

        uint width = Math.Clamp((uint)Width, 1, uint.MaxValue);
        uint height = Math.Clamp((uint)Height, 1, uint.MaxValue);

        if (swapChain is null)
        {
            swapChain = handler.VirtualView.GraphicsContext.CreateSwapChain(new()
            {
                Surface = Surface.Android(ANativeWindowFromSurface(JniEnvironment.EnvironmentPointer, Holder!.Surface!.Handle), width, height),
                ColorFormat = ZenithViewHelper.ColorFormat
            });
        }
        else if (swapChain.Desc.Surface.Width != width || swapChain.Desc.Surface.Height != height)
        {
            swapChain.Resize(width, height);
        }
    }

    public void Tick()
    {
        if (!ValidateSurface() || swapChain is null)
        {
            return;
        }

        handler.VirtualView.OnUpdateRequested();
        handler.VirtualView.OnRenderRequested(swapChain.CurrentTexture);
    }

    public void Present()
    {
        if (!ValidateSurface())
        {
            return;
        }

        swapChain?.Present();
    }

    public void ReleaseResources()
    {
        swapChain?.Dispose();
        swapChain = null;
    }

    private bool ValidateSurface()
    {
        bool isValid = Holder?.Surface?.IsValid ?? false;

        if (!isValid)
        {
            ReleaseResources();
        }

        return isValid;
    }
}
