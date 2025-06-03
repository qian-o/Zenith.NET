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

    /// <summary>
    /// The type of the texture.
    /// </summary>
    public TextureType Type { get; set; }

    /// <summary>
    /// The format of individual texture elements.
    /// </summary>
    public PixelFormat Format { get; set; }

    /// <summary>
    /// The width of the texture in pixels.
    /// </summary>
    public uint Width { get; set; }

    /// <summary>
    /// The height of the texture in pixels.
    /// </summary>
    public uint Height { get; set; }

    /// <summary>
    /// The depth of the texture in pixels.
    /// </summary>
    public uint Depth { get; set; }

    /// <summary>
    /// The number of array layers.
    /// </summary>
    public uint ArrayLayers { get; set; }

    /// <summary>
    /// The number of mipmap levels.
    /// </summary>
    public uint MipLevels { get; set; }

    /// <summary>
    /// The number of samples.
    /// </summary>
    public SampleCount SampleCount { get; set; }

    /// <summary>
    /// Indicates the intended use of the texture.
    /// </summary>
    public TextureUsageFlags Flags { get; set; }

    public readonly bool Validate()
    {
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

        if (Flags is TextureUsageFlags.None)
        {
            return false;
        }

        return true;
    }
}
