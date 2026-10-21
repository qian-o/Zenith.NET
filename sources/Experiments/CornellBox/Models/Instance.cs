using System.Numerics;
using System.Runtime.InteropServices;

namespace CornellBox.Models;

[StructLayout(LayoutKind.Explicit, Size = 80)]
internal struct Instance
{
    [FieldOffset(0)]
    public Matrix4x4 ObjectToWorld;

    [FieldOffset(64)]
    public uint FirstIndex;
}
