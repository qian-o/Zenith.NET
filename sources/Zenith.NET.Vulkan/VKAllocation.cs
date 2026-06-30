using Silk.NET.Vulkan;

namespace Zenith.NET.Vulkan;

internal readonly struct VKAllocation(DeviceMemory deviceMemory, ulong offsetInBytes, bool isOwned)
{
    public readonly DeviceMemory DeviceMemory = deviceMemory;

    public readonly ulong OffsetInBytes = offsetInBytes;

    public readonly bool IsOwned = isOwned;
}
