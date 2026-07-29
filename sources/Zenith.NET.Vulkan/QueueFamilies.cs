using Silk.NET.Vulkan;

namespace Zenith.NET.Vulkan;

internal readonly unsafe struct QueueFamilies : IDisposable
{
    private readonly ZenithMarshal.Scope scope = new();

    public readonly SharingMode SharingMode;

    public readonly uint IndexCount;

    public readonly uint* Indices;

    public QueueFamilies(ReadOnlySpan<uint> indices)
    {
        SharingMode = indices.Length is 1 ? SharingMode.Exclusive : SharingMode.Concurrent;
        IndexCount = (uint)indices.Length;
        Indices = (uint*)ZenithMarshal.AllocateAndFill(scope, indices);
    }

    public void Dispose()
    {
        scope.Dispose();
    }
}
