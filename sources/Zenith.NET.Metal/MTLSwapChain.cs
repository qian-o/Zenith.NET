using Metal.NET;

namespace Zenith.NET.Metal;

internal class MTLSwapChain : SwapChain
{
    public CAMetalLayer MetalLayer;

    public CAMetalDrawable MetalDrawable;

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

    protected override void PresentImpl()
    {
        Queue.Metal().CommandQueue.SignalDrawable(MetalDrawable);

        MetalDrawable.Present();
        MetalDrawable.Dispose();
        MetalDrawable = NSAutorelease.NextDrawable(MetalLayer);
    }

    protected override void ResizeImpl()
    {
        MetalLayer.DrawableSize = new(Desc.Surface.Width, Desc.Surface.Height);

        MetalDrawable.Dispose();
        MetalDrawable = NSAutorelease.NextDrawable(MetalLayer);
    }

    protected override void RefreshImpl()
    {
        drawable?.Dispose();
        drawable = null;

        MetalDrawable.Dispose();

        Initialize();
    }

    protected override void SetResourceName(string name)
    {
    }

    protected override void Destroy()
    {
        drawable?.Dispose();

        MetalDrawable.Dispose();
    }

    private void Initialize()
    {
        MetalLayer = new(Desc.Surface.Handles[0])
        {
            Device = Context.Device,
            PixelFormat = MTLFormats.Metal(Desc.Format),
            FramebufferOnly = false,
            DrawableSize = new(Desc.Surface.Width, Desc.Surface.Height)
        };

        if (OperatingSystem.IsMacOS() || OperatingSystem.IsMacCatalyst())
        {
            MetalLayer.DisplaySyncEnabled = Desc.IsVerticalSyncEnabled;
        }

        MetalDrawable = NSAutorelease.NextDrawable(MetalLayer);
    }
}
