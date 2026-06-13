namespace Zenith.NET.Metal;

internal class MTLTextureView : TextureView
{
    public MtlTexture Texture;

    public MTLTextureView(MTLGraphicsContext context, TextureViewDesc desc) : base(context, desc)
    {
        Texture = desc.Texture.Metal().Texture.MakeTextureView(MTLFormats.Metal(desc.Format),
                                                               MTLFormats.Metal(desc.Type, desc.Texture.Desc.SampleCount),
                                                               new(desc.Range.BaseMipLevel, desc.Range.LevelCount),
                                                               new(desc.Range.BaseArrayLayer, desc.Range.LayerCount));

        SampledHandle = Texture.GpuResourceID.Impl.ToHandle();
        StorageHandle = Texture.GpuResourceID.Impl.ToHandle();
    }

    public override ResourceHandle SampledHandle { get; }

    public override ResourceHandle StorageHandle { get; }

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
