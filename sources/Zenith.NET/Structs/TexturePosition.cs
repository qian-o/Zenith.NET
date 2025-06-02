namespace Zenith.NET;

public record struct TexturePosition
{
    public uint X { get; set; }

    public uint Y { get; set; }

    public uint Z { get; set; }

    public CubeMapFace Face { get; set; }

    public uint ArrayLayer { get; set; }

    public uint MipLevel { get; set; }
}
