namespace Zenith.NET;

public record struct TextureSubresourceDesc
{
    public Texture Texture;

    public TextureType Type;

    public uint MipLevel;

    public uint FirstLayer;

    public uint LayerCount;
}
