namespace Zenith.NET.Metal;

internal class MTLTextureView(MTLGraphicsContext context, TextureViewDesc desc) : TextureView(context, desc)
{
    public MtlTexture Texture = desc.Texture.Metal().Texture.MakeTextureView(MTLFormats.Metal(desc.Format),
                                                                             MTLFormats.Metal(desc.Type),
                                                                             new(desc.Range.BaseMipLevel, desc.Range.LevelCount),
                                                                             new(desc.Range.BaseArrayLayer, desc.Range.LayerCount));

    public override ResourceHandle SampledHandle => Texture.GpuResourceID.Impl.ToResourceHandle();

    public override ResourceHandle StorageHandle => Texture.GpuResourceID.Impl.ToResourceHandle();

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
        Texture.Dispose();
    }
}
