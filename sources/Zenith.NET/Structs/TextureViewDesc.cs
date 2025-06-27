namespace Zenith.NET;

public record struct TextureViewDesc
{
    public Texture Texture;

    public uint MipLevel;

    public uint FirstLayer;

    public uint LayerCount;

    public TextureUsageFlags? Flags;
}
