using Silk.NET.Vulkan;

namespace Zenith.NET.Vulkan;

internal readonly struct VKAllocation(DeviceMemory deviceMemory, ulong offsetInBytes, bool ownsResource, bool ownsMemory)
{
    public readonly DeviceMemory DeviceMemory = deviceMemory;

    public readonly ulong OffsetInBytes = offsetInBytes;

    public readonly bool OwnsResource = ownsResource;

    public readonly bool OwnsMemory = ownsMemory;
}
