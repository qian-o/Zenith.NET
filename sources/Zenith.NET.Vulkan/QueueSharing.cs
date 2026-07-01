using Silk.NET.Vulkan;

namespace Zenith.NET.Vulkan;

internal readonly unsafe struct QueueSharing : IDisposable
{
    private readonly ZenithMarshal.Scope scope = new();

    public readonly SharingMode Mode;

    public readonly uint Count;

    public readonly uint* Indices;

    public QueueSharing(ReadOnlySpan<uint> indices)
    {
        Mode = indices.Length is 1 ? SharingMode.Exclusive : SharingMode.Concurrent;
        Count = (uint)indices.Length;
        Indices = (uint*)ZenithMarshal.AllocateAndFill(scope, indices);
    }

    public void Dispose()
    {
        scope.Dispose();
    }
}
