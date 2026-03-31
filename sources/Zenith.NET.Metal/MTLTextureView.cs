using Metal.NET;

namespace Zenith.NET.Metal;

internal class MTLTextureView : TextureView
{
    public MtlTexture Texture;

    public MTLTextureView(MTLGraphicsContext context, TextureViewDesc desc) : base(context, desc)
    {
        MTLTextureViewDescriptor descriptor = new()
        {
            PixelFormat = MTLFormats.Metal(desc.Texture.Desc.Format).PixelFormat,
            TextureType = Resolve(desc),
            LevelRange = new(desc.FirstMipLevel, desc.MipLevelCount),
            SliceRange = new(ZenithHelper.FlattenArrayLayerRange(desc).FlattenArrayLayerIndex, ZenithHelper.FlattenArrayLayerRange(desc).FlattenArrayLayerCount)
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

    private static MTLTextureType Resolve(TextureViewDesc desc)
    {
        return MTLFormats.Metal(desc.Texture.Desc.Type switch
        {
            TextureType.Texture1DArray when desc.ArrayLayerCount is 1 => TextureType.Texture1D,
            TextureType.Texture2DArray when desc.ArrayLayerCount is 1 => TextureType.Texture2D,
            TextureType.TextureCubeArray when desc.ArrayLayerCount is 1 => TextureType.TextureCube,
            _ => desc.Texture.Desc.Type
        });
    }
}
