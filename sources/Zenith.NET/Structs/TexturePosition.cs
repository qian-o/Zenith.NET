namespace Zenith.NET;

/// <summary>
/// Describes a position within a texture, including coordinates, face index, array layer, and mip level.
/// </summary>
public record struct TexturePosition
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TexturePosition"/> struct with default values.
    /// </summary>
    public TexturePosition()
    {
        X = 0;
        Y = 0;
        Z = 0;
        FaceIndex = 0;
        ArrayLayer = 0;
        MipLevel = 0;
    }

    /// <summary>
    /// The X coordinate of the texture position.
    /// </summary>
    public uint X { get; set; }

    /// <summary>
    /// The Y coordinate of the texture position.
    /// Only applicable for all texture types except <see cref="TextureType.Texture1D"/> and <see cref="TextureType.Texture1DArray"/>.
    /// </summary>
    public uint Y { get; set; }

    /// <summary>
    /// The Z coordinate of the texture position.
    /// Only applicable for 3D textures such as <see cref="TextureType.Texture3D"/>.
    /// </summary>
    public uint Z { get; set; }

    /// <summary>
    /// The face index of the texture.
    /// Only applicable for cube map textures such as <see cref="TextureType.TextureCube"/> or <see cref="TextureType.TextureCubeArray"/>.
    /// </summary>
    public uint FaceIndex { get; set; }

    /// <summary>
    /// The array layer of the texture.
    /// Only applicable for array textures such as <see cref="TextureType.Texture1DArray"/>, <see cref="TextureType.Texture2DArray"/>, or <see cref="TextureType.TextureCubeArray"/>.
    /// </summary>
    public uint ArrayLayer { get; set; }

    /// <summary>
    /// The mip level of the texture.
    /// </summary>
    public uint MipLevel { get; set; }
}
