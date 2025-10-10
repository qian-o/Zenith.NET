using System.Runtime.InteropServices;

namespace Zenith.NET;

public unsafe class MemoryOwner : DisposableObject
{
    private readonly List<nint> allocations = [];
    private readonly Lock @lock = new();

    internal nint Alloc<T>(ReadOnlySpan<T> data) where T : unmanaged
    {
        void* ptr = Alloc((uint)(data.Length * sizeof(T)));

        data.CopyTo(new Span<T>(ptr, data.Length));

        return (nint)ptr;
    }

    protected override void Destroy()
    {
        using Lock.Scope _ = @lock.EnterScope();

        foreach (nint ptr in allocations)
        {
            NativeMemory.Free((void*)ptr);
        }

        allocations.Clear();
    }

    private void* Alloc(uint sizeInBytes)
    {
        using Lock.Scope _ = @lock.EnterScope();

        void* ptr = NativeMemory.Alloc(sizeInBytes);

        allocations.Add((nint)ptr);

        return ptr;
    }
}
