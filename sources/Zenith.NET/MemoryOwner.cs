using System.Runtime.InteropServices;

namespace Zenith.NET;

public unsafe class MemoryOwner : DisposableObject
{
    private readonly List<nint> pointers = [];

    internal nint Native<T>(ReadOnlySpan<T> data) where T : unmanaged
    {
        nint pointer = (nint)NativeMemory.Alloc((uint)(data.Length * sizeof(T)));

        data.CopyTo(new Span<T>((void*)pointer, data.Length));

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
