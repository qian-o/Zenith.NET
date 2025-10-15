using System.Numerics;

namespace Zenith.NET;

public static class ClearValues
{
    public static readonly ClearValue Default = new()
    {
        ColorValues = [.. Enumerable.Repeat<Vector4>(new(0, 0, 0, 1), 8)],
        Depth = 1.0f,
        Stencil = 0,
        Flags = ClearFlags.All
    };

    public static readonly ClearValue ColorOnly = Default with
    {
        Flags = ClearFlags.Color
    };

    public static readonly ClearValue DepthOnly = Default with
    {
        Flags = ClearFlags.Depth
    };

    public static readonly ClearValue StencilOnly = Default with
    {
        Flags = ClearFlags.Stencil
    };

    public static readonly ClearValue None = Default with
    {
        Flags = ClearFlags.None
    };
}
