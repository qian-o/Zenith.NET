namespace Zenith.NET;

public record struct TextureViewDesc
{
    public Texture Texture;

    public uint FirstMipLevel;

    public uint MipLevelCount;

    public uint FirstArrayLayer;

    public uint ArrayLayerCount;
}
