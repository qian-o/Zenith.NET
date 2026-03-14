using System.Numerics;
using System.Runtime.InteropServices;

namespace SponzaScene.Models;

[StructLayout(LayoutKind.Explicit, Size = 32)]
internal struct PointLight
{
    [FieldOffset(0)]
    public Vector4 PositionAndRadius;    // XYZ = Position, W = Radius

    [FieldOffset(16)]
    public Vector4 ColorAndIntensity;    // XYZ = Color, W = Intensity
}
