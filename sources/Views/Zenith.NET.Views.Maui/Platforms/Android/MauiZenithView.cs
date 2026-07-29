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
                Format = ZenithViewHelper.DrawableFormat
            });
        }
        else if (swapChain.Desc.Surface.Width != width || swapChain.Desc.Surface.Height != height)
        {
            swapChain.Resize(width, height);
        }
    }

    public void Tick()
    {
        if (!ValidateSurface() || handler.VirtualView.GraphicsContext is null || swapChain is null)
        {
            return;
        }

        CommandBuffer commandBuffer = handler.VirtualView.GraphicsContext.GraphicsQueue.CommandBuffer();

        commandBuffer.Transition(swapChain.Drawable, default, TextureLayout.Undefined, TextureLayout.ColorAttachment);

        handler.VirtualView.OnUpdateRequested();
        handler.VirtualView.OnRenderRequested(commandBuffer, swapChain.Drawable);

        commandBuffer.Transition(swapChain.Drawable, default, TextureLayout.ColorAttachment, TextureLayout.Present);

        commandBuffer.Submit().Wait();
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
