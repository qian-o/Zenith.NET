using System.Numerics;
using System.Runtime.InteropServices;

namespace FluidTank.Models;

[StructLayout(LayoutKind.Explicit, Size = 32)]
internal struct SceneMaterial
{
    [FieldOffset(0)]
    public Vector3 Albedo;

    [FieldOffset(12)]
    public float Roughness;

    [FieldOffset(16)]
    public float Metallic;

    [FieldOffset(20)]
    public float Emission;
}
