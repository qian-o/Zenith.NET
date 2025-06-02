namespace Zenith.NET;

public record struct TexturePosition
{
    /// <summary>
    /// X coordinate of the texture position.
    /// </summary>
    public uint X { get; set; }

    /// <summary>
    /// Y coordinate of the texture position.
    /// Only applicable for all texture types except <see cref="TextureType.Texture1D"/> and <see cref="TextureType.Texture1DArray"/>.
    /// </summary>
    public uint Y { get; set; }

    /// <summary>
    /// Z coordinate of the texture position.
    /// Only applicable for 3D textures such as <see cref="TextureType.Texture3D"/>.
    /// </summary>
    public uint Z { get; set; }

    /// <summary>
    /// Indicates the face of the cube map texture.
    /// Only applicable for cube map textures such as <see cref="TextureType.TextureCube"/> or <see cref="TextureType.TextureCubeArray"/>.
    /// </summary>
    public CubeMapFace Face { get; set; }

    /// <summary>
    /// Indicates the array layer of the texture.
    /// Only applicable for array textures such as <see cref="TextureType.Texture1DArray"/>, <see cref="TextureType.Texture2DArray"/>, or <see cref="TextureType.TextureCubeArray"/>.
    /// </summary>
    public uint ArrayLayer { get; set; }

    /// <summary>
    /// Indicates the mip level of the texture.
    /// </summary>
    public uint MipLevel { get; set; }
}
