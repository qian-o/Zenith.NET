namespace Zenith.NET;

/// <summary>
/// Specifies how texture coordinates outside the [0.0, 1.0] range are handled.
/// </summary>
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
