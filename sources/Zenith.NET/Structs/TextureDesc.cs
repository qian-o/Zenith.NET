namespace Zenith.NET;

public record struct TextureDesc
{
    public TextureType Type;

    public PixelFormat Format;

    public uint Width;

    public uint Height;

    public uint Depth;

    public uint MipLevels;

    public uint ArrayLayers;

    public SampleCount SampleCount;

    public TextureUsages Usages;
}
