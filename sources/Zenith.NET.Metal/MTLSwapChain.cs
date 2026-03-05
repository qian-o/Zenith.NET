using Metal.NET;

namespace Zenith.NET.Metal;

internal class MTLSwapChain : SwapChain
{
    private readonly MTLSwapChainFrameBuffer swapChainFrameBuffer;

    public CAMetalLayer Layer = CAMetalLayer.Null;

    public CAMetalDrawable Drawable = CAMetalDrawable.Null;

    public MTLSwapChain(MTLGraphicsContext context, SwapChainDesc desc) : base(context, desc)
    {
        swapChainFrameBuffer = new(context, this);

        CreateSwapChain();
    }

    public new MTLGraphicsContext Context => (MTLGraphicsContext)base.Context;

    public override FrameBuffer FrameBuffer => swapChainFrameBuffer.Get(Desc.Surface.Width, Desc.Surface.Height, Drawable);

    public override void Present()
    {
        Drawable.Present();
        Drawable.Dispose();

        AcquireNextDrawable();
    }

    protected override void ResizeImpl()
    {
        Drawable.Dispose();

        Layer.DrawableSize = new(Desc.Surface.Width, Desc.Surface.Height);

        AcquireNextDrawable();
    }

    protected override void RefreshImpl()
    {
        CreateSwapChain();
    }

    protected override void SetResourceName(string name)
    {
    }

    protected override void Destroy()
    {
        DestroySwapChain();

        swapChainFrameBuffer.Dispose();
    }

    private void CreateSwapChain()
    {
        DestroySwapChain();

        Layer = new(Desc.Surface.Handles[0], NativeObjectOwnership.Borrowed)
        {
            Device = Context.Device,
            PixelFormat = MTLFormats.Metal(Desc.ColorTargetFormat),
            DrawableSize = new(Desc.Surface.Width, Desc.Surface.Height),
            FramebufferOnly = false
        };

        AcquireNextDrawable();
    }

    private void DestroySwapChain()
    {
        Drawable.Dispose();
        Layer.Dispose();
    }

    private void AcquireNextDrawable()
    {
        using NSAutoreleasePool _ = new();

        Drawable = Layer.NextDrawable();

        ObjectiveC.Retain(Drawable.NativePtr);
    }
}
