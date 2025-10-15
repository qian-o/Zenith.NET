using System.Runtime.InteropServices;

namespace Zenith.NET;

public unsafe class MemoryOwner : DisposableObject
{
    private readonly List<nint> pointers = [];

    internal nint Native<T>(ReadOnlySpan<T> data) where T : unmanaged
    {
        nint pointer = (nint)NativeMemory.Alloc(MemoryMarshal.SizeInBytes<T>((uint)data.Length));

        MemoryMarshal.Copy(data, pointer);

        pointers.Add(pointer);

        return pointer;
    }

    protected override void Destroy()
    {
        foreach (nint pointer in pointers)
        {
            NativeMemory.Free((void*)pointer);
        }

        pointers.Clear();
    }
}
