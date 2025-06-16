namespace Zenith.NET;

public record struct TextureDesc
{
    public TextureType Type { get; set; }

    public PixelFormat Format { get; set; }

    public uint Width { get; set; }

    public uint Height { get; set; }

    public uint Depth { get; set; }

    public uint ArrayLayers { get; set; }

    public uint MipLevels { get; set; }

    public SampleCount SampleCount { get; set; }

    public TextureUsageFlags Flags { get; set; }
}
