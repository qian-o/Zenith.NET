using System.Numerics;
using System.Runtime.InteropServices;

namespace CornellBox.Models;

[StructLayout(LayoutKind.Explicit, Size = 32)]
internal struct Vertex
{
    [FieldOffset(0)]
    public Vector3 Position;

    [FieldOffset(16)]
    public Vector3 Normal;

    [FieldOffset(28)]
    public uint MaterialId;
}
