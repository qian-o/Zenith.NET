namespace Zenith.NET;

public record struct TextureDesc : IDesc
{
    public TextureDesc()
    {
        Type = TextureType.Texture1D;
        Format = PixelFormat.R8UNorm;
        Width = 0;
        Height = 0;
        Depth = 0;
        ArrayLayers = 1;
        MipLevels = 1;
        SampleCount = SampleCount.Count1;
        Flags = TextureUsageFlags.None;
    }

    public TextureType Type { get; set; }

    public PixelFormat Format { get; set; }

    public uint Width { get; set; }

    public uint Height { get; set; }

    public uint Depth { get; set; }

    public uint ArrayLayers { get; set; }

    public uint MipLevels { get; set; }

    public SampleCount SampleCount { get; set; }

    public TextureUsageFlags Flags { get; set; }

    public readonly bool Validate()
    {
        if (!Enum.IsDefined(Type))
        {
            return false;
        }

        if (!Enum.IsDefined(Format))
        {
            return false;
        }

        if (Type is TextureType.Texture1D or TextureType.Texture1DArray && Width is 0)
        {
            return false;
        }

        if (Type is TextureType.Texture2D or TextureType.Texture2DArray && (Width is 0 || Height is 0))
        {
            return false;
        }

        if (Type is TextureType.Texture3D && (Width is 0 || Height is 0 || Depth is 0))
        {
            return false;
        }

        if (Type is TextureType.TextureCube or TextureType.TextureCubeArray && (Width is 0 || Height is 0))
        {
            return false;
        }

        if (Type is TextureType.Texture1DArray or TextureType.Texture2DArray or TextureType.TextureCubeArray
            && ArrayLayers is 0)
        {
            return false;
        }

        if (MipLevels is 0)
        {
            return false;
        }

        if (!Enum.IsDefined(SampleCount))
        {
            return false;
        }

        if (Flags is TextureUsageFlags.None)
        {
            return false;
        }

        return true;
    }
}
