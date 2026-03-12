using System.Numerics;
using System.Runtime.InteropServices;

namespace SponzaScene.Models;

[StructLayout(LayoutKind.Explicit, Size = 144)]
internal struct CSMData
{
    [FieldOffset(0)]
    public Matrix4x4 View;

    [FieldOffset(64)]
    public Matrix4x4 Projection;

    [FieldOffset(128)]
    public float NearPlane;

    [FieldOffset(132)]
    public float FarPlane;
}
