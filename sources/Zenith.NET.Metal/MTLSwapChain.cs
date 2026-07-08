using Metal.NET;

namespace Zenith.NET.Metal;

internal class MTLSwapChain : SwapChain
{
    public CAMetalLayer MetalLayer = CAMetalLayer.Null;

    public CAMetalDrawable MetalDrawable = CAMetalDrawable.Null;

    private MTLTexture? drawable;

    public MTLSwapChain(MTLGraphicsContext context, SwapChainDesc desc) : base(context, desc)
    {
        Initialize();
    }

    public new MTLGraphicsContext Context => (MTLGraphicsContext)base.Context;

    public override Texture Drawable
    {
        get
        {
            if (drawable is null || drawable.Texture.NativePtr != MetalDrawable.Texture.NativePtr)
            {
                TextureDesc desc = new()
                {
                    Type = TextureType.Texture2D,
                    Format = Desc.Format,
                    Width = Desc.Surface.Width,
                    Height = Desc.Surface.Height,
                    Depth = 1,
                    MipLevels = 1,
                    ArrayLayers = 1,
                    SampleCount = SampleCount.Count1,
                    Usages = TextureUsages.ColorAttachment | TextureUsages.TransferDst
                };

                drawable?.Dispose();
                drawable = new(Context, desc, MetalDrawable.Texture.Retain());
            }

            return drawable;
        }
    }

    public override nint GetNativeObject(NativeObjectType type)
    {
        return 0;
    }

    public override void Present()
    {
        Context.GraphicsQueue.Metal().CommandQueue.SignalDrawable(MetalDrawable);

        MetalDrawable.Present();

        Context.GraphicsQueue.Timeline.Signal().Wait();

        MetalDrawable.Dispose();
        MetalDrawable = NSAutorelease.Own(MetalLayer.NextDrawable);
    }

    protected override void ResizeImpl()
    {
        MetalLayer.DrawableSize = new(Desc.Surface.Width, Desc.Surface.Height);

        MetalDrawable.Dispose();
        MetalDrawable = NSAutorelease.Own(MetalLayer.NextDrawable);
    }

    protected override void RefreshImpl()
    {
        drawable?.Dispose();
        drawable = null;

        MetalDrawable.Dispose();
        MetalLayer.Dispose();

        Initialize();
    }

    protected override void SetResourceName(string name)
    {
    }

    protected override void Destroy()
    {
        drawable?.Dispose();

        MetalDrawable.Dispose();
        MetalLayer.Dispose();
    }

    private void Initialize()
    {
        MetalLayer = new(Desc.Surface.Handles[0], NativeObjectOwnership.Borrowed)
        {
            Device = Context.Device,
            PixelFormat = MTLFormats.Metal(Desc.Format).PixelFormat,
            FramebufferOnly = false,
            DrawableSize = new(Desc.Surface.Width, Desc.Surface.Height)
        };

        MetalDrawable = NSAutorelease.Own(MetalLayer.NextDrawable);
    }
}
