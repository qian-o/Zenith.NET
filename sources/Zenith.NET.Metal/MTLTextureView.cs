using Metal.NET;

namespace Zenith.NET.Metal;

internal class MTLTextureView : TextureView
{
    public MtlTexture Texture;

    public MTLTextureView(MTLGraphicsContext context, TextureViewDesc desc) : base(context, desc)
    {
        TextureSubresourceRange range = desc.Range;

        MTLTextureViewDescriptor descriptor = new()
        {
            PixelFormat = MTLFormats.Metal(desc.Format).PixelFormat,
            TextureType = MTLFormats.Metal(desc.Type),
            LevelRange = new(range.BaseMipLevel, range.LevelCount),
            SliceRange = new(range.BaseArrayLayer, range.LayerCount)
        };

        Texture = desc.Texture.Metal().Texture.MakeTextureView(descriptor);
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
