using Silk.NET.Direct3D12;

namespace Zenith.NET.DirectX12;

internal readonly struct DXDescriptorToken(uint slot, uint incrementSize, CpuDescriptorHandle cpuStartHandle, GpuDescriptorHandle gpuStartHandle)
{
    public uint Slot => slot;

    public ResourceHandle ResourceHandle => new(slot, 0);

    public CpuDescriptorHandle CpuHandle => new() { Ptr = (slot * incrementSize) + cpuStartHandle.Ptr };

    public GpuDescriptorHandle GpuHandle => new() { Ptr = (slot * incrementSize) + gpuStartHandle.Ptr };
}