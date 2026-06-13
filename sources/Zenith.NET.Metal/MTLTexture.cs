using Metal.NET;

namespace Zenith.NET.Metal;

internal class MTLTexture : Texture
{
    public MtlTexture Texture;

    public MTLTexture(MTLGraphicsContext context, TextureDesc desc) : base(context, desc)
    {
        context.Register(Texture = context.Device.MakeTexture(Descriptor(desc)));

        View = new(context, new()
        {
            Texture = this,
            Type = desc.Type,
            Format = desc.Format,
            Range = TextureSubresourceRange.All(this)
        });
    }

    public MTLTexture(MTLGraphicsContext context, TextureDesc desc, MtlTexture texture) : base(context, desc)
    {
        context.Register(Texture = texture);

        View = new(context, new()
        {
            Texture = this,
            Type = desc.Type,
            Format = desc.Format,
            Range = TextureSubresourceRange.All(this)
        });
    }

    public new MTLGraphicsContext Context => (MTLGraphicsContext)base.Context;

    public MTLTextureView View { get; }

    public override ResourceHandle SampledHandle => View.SampledHandle;

    public override ResourceHandle StorageHandle => View.StorageHandle;

    public override nint GetNativeObject(NativeObjectType type)
    {
        return 0;
    }

    protected override void SetResourceName(string name)
    {
        Texture.Label = name;
    }

    protected override void Destroy()
    {
        Context.Unregister(Texture);

        View.Dispose();
        Texture.Dispose();
    }

    public static MTLTextureDescriptor Descriptor(TextureDesc desc)
    {
        return new()
        {
            TextureType = MTLFormats.Metal(desc.Type),
            PixelFormat = MTLFormats.Metal(desc.Format),
            Width = desc.Width,
            Height = desc.Height,
            Depth = desc.Depth,
            MipmapLevelCount = desc.MipLevels,
            SampleCount = MTLFormats.Metal(desc.SampleCount),
            ArrayLength = desc.ArrayLayers,
            ResourceOptions = MTLResourceOptions.CPUCacheModeDefaultCache | MTLResourceOptions.StorageModePrivate | MTLResourceOptions.HazardTrackingModeUntracked,
            Usage = MTLFormats.Metal(desc.Usages),
            AllowGPUOptimizedContents = true
        };
    }
}
