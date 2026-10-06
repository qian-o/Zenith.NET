using System.Numerics;
using System.Runtime.InteropServices;

namespace CornellBox.Models;

[StructLayout(LayoutKind.Explicit, Size = 32)]
internal struct Material
{
    [FieldOffset(0)]
    public Vector3 Albedo;

    [FieldOffset(12)]
    public float Emission;

    [FieldOffset(16)]
    public float Metallic;

    [FieldOffset(20)]
    public float Roughness;

    [FieldOffset(24)]
    public SurfacePattern Pattern;

    [FieldOffset(28)]
    public float PatternScale;
}
