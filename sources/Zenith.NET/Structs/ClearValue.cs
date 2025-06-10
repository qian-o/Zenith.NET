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

    public Vector4[] ColorValues { get; set; }

    public float Depth { get; set; }

    public byte Stencil { get; set; }

    public ClearFlags Flags { get; set; }
}
