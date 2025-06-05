using System.Numerics;

namespace Zenith.NET;

/// <summary>
/// Describes clear values for color, depth, and stencil buffers used during render target or depth/stencil clears.
/// </summary>
public record struct ClearValue
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ClearValue"/> struct with default values.
    /// </summary>
    public ClearValue()
    {
        ColorValues = [];
        Depth = 1.0f;
        Stencil = 0;
        Flags = ClearFlags.All;
    }

    /// <summary>
    /// The array of clear color values to use when clearing each color attachment.
    /// </summary>
    public Vector4[] ColorValues { get; set; }

    /// <summary>
    /// The depth clear value to use when clearing a depth/stencil attachment.
    /// </summary>
    public float Depth { get; set; }

    /// <summary>
    /// The stencil clear value used when clearing a depth/stencil attachment.
    /// </summary>
    public byte Stencil { get; set; }

    /// <summary>
    /// The flags that specify which buffers to clear.
    /// </summary>
    public ClearFlags Flags { get; set; }
}
