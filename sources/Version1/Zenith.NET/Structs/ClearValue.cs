using System.Numerics;

namespace Zenith.NET;

public record struct ClearValue
{
    public Vector4[] ColorValues;

    public float Depth;

    public byte Stencil;

    public ClearFlags Flags;
}
