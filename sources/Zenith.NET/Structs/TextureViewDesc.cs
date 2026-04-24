namespace Zenith.NET;

public record struct TextureViewDesc
{
    public Texture Texture;

    public TextureType Type;

    public PixelFormat Format;

    public TextureSubresourceRange Range;
}
