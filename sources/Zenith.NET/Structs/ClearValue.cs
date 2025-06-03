using System.Numerics;

namespace Zenith.NET;

public record struct ClearValue
{
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
