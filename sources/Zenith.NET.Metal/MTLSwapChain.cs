using Metal.NET;

namespace Zenith.NET.Metal;

internal class MTLSwapChain : SwapChain
{
    private readonly MTLSwapChainTargets swapChainTargets;

    public CAMetalLayer Layer = CAMetalLayer.Null;

    public CAMetalDrawable Drawable = CAMetalDrawable.Null;

    public MTLSwapChain(MTLGraphicsContext context, SwapChainDesc desc) : base(context, desc)
    {
        swapChainTargets = new(context, this);

        CreateSwapChain();
    }

    public new MTLGraphicsContext Context => (MTLGraphicsContext)base.Context;

    public override uint Width => CurrentColorTarget.Desc.Width;

    public override uint Height => CurrentColorTarget.Desc.Height;

    public override Texture CurrentColorTarget
    {
        get
        {
            swapChainTargets.EnsureTargets(Desc.Surface.Width, Desc.Surface.Height, Drawable);

            return swapChainTargets.ColorTarget;
        }
    }

    public override Texture? CurrentDepthStencilTarget
    {
        get
        {
            swapChainTargets.EnsureTargets(Desc.Surface.Width, Desc.Surface.Height, Drawable);

            return swapChainTargets.DepthStencilTarget;
        }
    }

    public override Output Output => new()
    {
        ColorAttachments = [Desc.ColorTargetFormat],
        DepthStencilAttachment = Desc.DepthStencilTargetFormat,
        SampleCount = SampleCount.Count1
    };

    public override void Present()
    {
        Drawable.Present();
        Drawable.Dispose();

        Drawable = NSAutorelease.Own(Layer.NextDrawable);
    }

    protected override void ResizeImpl()
    {
        Drawable.Dispose();
        swapChainTargets.DestroyTargets();

        Layer.DrawableSize = new(Desc.Surface.Width, Desc.Surface.Height);

        Drawable = NSAutorelease.Own(Layer.NextDrawable);
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

        swapChainTargets.Dispose();
    }

    private void CreateSwapChain()
    {
        DestroySwapChain();

        Layer = new(Desc.Surface.Handles[0], NativeObjectOwnership.Borrowed)
        {
            Device = Context.Device,
            PixelFormat = MTLFormats.Metal(Desc.ColorTargetFormat).PixelFormat,
            FramebufferOnly = false,
            DrawableSize = new(Desc.Surface.Width, Desc.Surface.Height)
        };

        Drawable = NSAutorelease.Own(Layer.NextDrawable);
    }

    private void DestroySwapChain()
    {
        Drawable.Dispose();
        Layer.Dispose();
    }
}
