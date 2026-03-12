using System.Numerics;
using System.Runtime.InteropServices;

namespace SponzaScene.Models;

[StructLayout(LayoutKind.Explicit, Size = 32)]
internal struct PointLight
{
    [FieldOffset(0)]
    public Vector3 Position;

    [FieldOffset(12)]
    public float Radius;

    [FieldOffset(16)]
    public Vector3 Color;

    [FieldOffset(28)]
    public float Intensity;
}
