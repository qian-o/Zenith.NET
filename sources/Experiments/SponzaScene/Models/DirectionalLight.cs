using System.Numerics;
using System.Runtime.InteropServices;

namespace SponzaScene.Models;

[StructLayout(LayoutKind.Explicit, Size = 32)]
internal struct DirectionalLight
{
    [FieldOffset(0)]
    public Vector3 Direction;

    [FieldOffset(12)]
    public float Intensity;

    [FieldOffset(16)]
    public Vector3 Color;
}