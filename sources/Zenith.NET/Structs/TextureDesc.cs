namespace Zenith.NET;

public record struct TextureDesc
{
    public TextureType Type;

    public PixelFormat Format;

    public uint Width;

    public uint Height;

    public uint Depth;

    public uint ArrayLayers;

    public uint MipLevels;

    public SampleCount SampleCount;

    public TextureUsageFlags Flags;
}
