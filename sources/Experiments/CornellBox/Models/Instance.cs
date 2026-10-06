using System.Numerics;
using System.Runtime.InteropServices;

namespace CornellBox.Models;

[StructLayout(LayoutKind.Explicit, Size = 144)]
internal struct Instance
{
    [FieldOffset(0)]
    public Matrix4x4 ObjectToWorld;

    [FieldOffset(64)]
    public Matrix4x4 PreviousObjectToWorld;

    [FieldOffset(128)]
    public uint FirstIndex;
}
