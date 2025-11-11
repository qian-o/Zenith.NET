namespace Zenith.NET;

public record struct TextureViewDesc
{
    public Texture Texture;

    public uint FirstMipLevel;

    public uint MipLevelCount;

    public uint FirstLayer;

    public uint LayerCount;

    public uint FirstFace;

    public uint FaceCount;
}
