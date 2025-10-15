namespace Zenith.NET;

public record struct TextureViewDesc
{
    public Texture Texture;

    public uint FirstLayer;

    public uint LayerCount;

    public uint FirstMipLevel;

    public uint MipLevelCount;
}
