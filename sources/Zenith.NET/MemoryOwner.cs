using System.Runtime.InteropServices;

namespace Zenith.NET;

public unsafe class MemoryOwner : DisposableObject
{
    private readonly List<nint> allocations = [];

    internal nint Native<T>(ReadOnlySpan<T> data) where T : unmanaged
    {
        void* ptr = NativeMemory.Alloc((uint)(data.Length * sizeof(T)));

        data.CopyTo(new Span<T>(ptr, data.Length));

        allocations.Add((nint)ptr);

        return (nint)ptr;
    }

    protected override void Destroy()
    {
        foreach (nint ptr in allocations)
        {
            NativeMemory.Free((void*)ptr);
        }

        allocations.Clear();
    }
}
