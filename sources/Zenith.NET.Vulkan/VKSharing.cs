using Silk.NET.Vulkan;

namespace Zenith.NET.Vulkan;

internal unsafe readonly struct VKSharing(SharingMode mode, uint* indices, uint count)
{
    public readonly SharingMode Mode = mode;

    public readonly uint* Indices = indices;

    public readonly uint Count = count;
}
