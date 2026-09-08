using System.Numerics;
using System.Runtime.InteropServices;

namespace Sponza.Models;

[StructLayout(LayoutKind.Explicit, Size = 48)]
internal struct SceneVertex
{
    [FieldOffset(0)]
    public Vector3 Position;

    [FieldOffset(12)]
    public Vector3 Normal;

    [FieldOffset(24)]
    public Vector4 Tangent;

    [FieldOffset(40)]
    public Vector2 TexCoord;
}
