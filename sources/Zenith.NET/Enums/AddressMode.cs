namespace Zenith.NET;

public enum AddressMode
{
    /// <summary>
    /// Texture coordinates are wrapped upon overflow.
    /// </summary>
    Wrap,

    /// <summary>
    /// Texture coordinates are mirrored upon overflow.
    /// </summary>
    Mirror,

    /// <summary>
    /// Texture coordinates are clamped to the maximum or minimum values upon overflow.
    /// </summary>
    Clamp,

    /// <summary>
    /// The texture coordinates return the predefined border color defined in the sampler.
    /// </summary>
    Border
}
