using System.Runtime.InteropServices;
using Zenith.NET;

namespace CornellBox.Models;

[StructLayout(LayoutKind.Explicit, Size = 256)]
internal struct OutputConstants
{
    [FieldOffset(0)]
    public ResourceHandle Input;

    [FieldOffset(8)]
    public ResourceHandle Overlay;

    [FieldOffset(16)]
    public uint FrameIndex;
}
