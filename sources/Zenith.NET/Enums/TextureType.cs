namespace Zenith.NET;

/// <summary>
/// Specifies the dimensionality and array-ness of a texture resource.
/// </summary>
public enum TextureType
{
    /// <summary>
    /// A one-dimensional texture.
    /// </summary>
    Texture1D,

    /// <summary>
    /// A one-dimensional texture array.
    /// </summary>
    Texture1DArray,

    /// <summary>
    /// A two-dimensional texture.
    /// </summary>
    Texture2D,

    /// <summary>
    /// A two-dimensional texture array.
    /// </summary>
    Texture2DArray,

    /// <summary>
    /// A three-dimensional texture.
    /// </summary>
    Texture3D,

    /// <summary>
    /// A cube texture.
    /// </summary>
    TextureCube,

    /// <summary>
    /// A cube texture array.
    /// </summary>
    TextureCubeArray
}
