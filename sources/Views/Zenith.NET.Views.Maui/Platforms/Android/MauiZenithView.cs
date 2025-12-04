using System.Runtime.InteropServices;
using Android.Graphics;
using Android.Views;
using Java.Interop;
using static Android.Views.Choreographer;

namespace Zenith.NET.Views.Maui.Platforms.Android;

internal partial class MauiZenithView : SurfaceView, ISurfaceHolderCallback, IFrameCallback
{
    [LibraryImport("android", EntryPoint = "ANativeWindow_fromSurface")]
    private static partial nint ANativeWindowFromSurface(nint env, nint surface);

    private readonly ViewTimer timer = new();

    private SwapChain? swapChain;

    public MauiZenithView(ZenithViewHandler handler) : base(handler.Context)
    {
        SetWillNotDraw(false);

        Holder?.AddCallback(this);

        ViewAttachedToWindow += (_, _) => timer.Start();

        ViewDetachedFromWindow += (_, _) =>
        {
            timer.Stop();

            Destroy();

            timer.Reset();
        };

        ZenithView = handler.VirtualView;
    }

    public static Output Output { get; } = new()
    {
        ColorAttachments = [PixelFormat.R8G8B8A8UNorm],
        DepthStencilAttachment = PixelFormat.D24UNormS8UInt,
        SampleCount = SampleCount.Count1
    };

    public ZenithView ZenithView { get; }

    public void Destroy()
    {
        swapChain?.Dispose();
        swapChain = null;
    }

    void ISurfaceHolderCallback.SurfaceChanged(ISurfaceHolder holder, Format format, int width, int height)
    {
        swapChain?.Resize((uint)width, (uint)height);
    }

    void ISurfaceHolderCallback.SurfaceCreated(ISurfaceHolder holder)
    {
        Instance?.PostFrameCallback(this);
    }

    void ISurfaceHolderCallback.SurfaceDestroyed(ISurfaceHolder holder)
    {
        Destroy();

        Instance?.RemoveFrameCallback(this);
    }

    void IFrameCallback.DoFrame(long frameTimeNanos)
    {
        if (ZenithView.GraphicsContext is null)
        {
            return;
        }

        uint width = Math.Clamp((uint)Width, 1, uint.MaxValue);
        uint height = Math.Clamp((uint)Height, 1, uint.MaxValue);

        swapChain ??= ZenithView.GraphicsContext.CreateSwapChain(new()
        {
            Surface = Surface.Android(ANativeWindowFromSurface(JniEnvironment.EnvironmentPointer, Holder?.Surface?.Handle ?? 0), width, height),
            ColorTargetFormat = PixelFormat.R8G8B8A8UNorm,
            DepthStencilTargetFormat = PixelFormat.D24UNormS8UInt
        });

        ZenithView.OnUpdateRequested(new(timer.GetAndRestartUpdate(), timer.TotalSeconds));
        ZenithView.OnRenderRequested(new(timer.GetAndRestartRender(), timer.TotalSeconds, swapChain.FrameBuffer));

        swapChain.Present();

        Instance?.PostFrameCallback(this);
    }
}
