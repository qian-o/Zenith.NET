using Silk.NET.Direct3D12;

namespace Zenith.NET.DirectX12;

internal readonly struct DXDescriptorToken(uint slot, uint incrementSize, CpuDescriptorHandle cpuStartHandle, GpuDescriptorHandle gpuStartHandle)
{
    public readonly uint Slot = slot;

    public readonly ResourceHandle ResourceHandle = new(slot, 0);

    public readonly CpuDescriptorHandle CpuHandle = new() { Ptr = (slot * incrementSize) + cpuStartHandle.Ptr };

    public readonly GpuDescriptorHandle GpuHandle = new() { Ptr = (slot * incrementSize) + gpuStartHandle.Ptr };
}