using System.Numerics;

namespace Zenith.NET;

public record struct ClearValue
{
    public Vector4[] ColorValues { get; set; }

    public float Depth { get; set; }

    public byte Stencil { get; set; }

    public ClearFlags Flags { get; set; }
}
