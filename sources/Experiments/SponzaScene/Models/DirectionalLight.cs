using System.Numerics;
using System.Runtime.InteropServices;

namespace SponzaScene.Models;

[StructLayout(LayoutKind.Explicit, Size = 32)]
internal struct DirectionalLight
{
    [FieldOffset(0)]
    public Vector4 DirectionAndIntensity; // XYZ = Direction, W = Intensity

    [FieldOffset(16)]
    public Vector4 ColorAndPadding;       // XYZ = Color, W = unused
}