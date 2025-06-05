namespace Zenith.NET;

/// <summary>
/// Describes a texture resource, including its type, format, dimensions, mip levels, sample count, and usage flags.
/// </summary>
public record struct TextureDesc : IDesc
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TextureDesc"/> struct with default values.
    /// </summary>
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
    /// The width of the texture, in pixels.
    /// </summary>
    public uint Width { get; set; }

    /// <summary>
    /// The height of the texture, in pixels.
    /// </summary>
    public uint Height { get; set; }

    /// <summary>
    /// The depth of the texture, in pixels.
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
    /// The number of samples per texel.
    /// </summary>
    public SampleCount SampleCount { get; set; }

    /// <summary>
    /// Indicates the intended usage of the texture.
    /// </summary>
    public TextureUsageFlags Flags { get; set; }

    /// <summary>
    /// Validates the current <see cref="TextureDesc"/> instance.
    /// </summary>
    /// <returns><c>true</c> if valid; otherwise, <c>false</c>.</returns>
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
