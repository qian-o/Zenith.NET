using Silk.NET.Direct3D12;

namespace Zenith.NET.DirectX12;

internal readonly struct DXDescriptorToken(uint slot, uint incrementSize, CpuDescriptorHandle cpuStart, GpuDescriptorHandle gpuStart)
{
    public uint Slot => slot;

    public ResourceHandle ResourceHandle => new(slot, 0);

    public CpuDescriptorHandle CpuHandle => new() { Ptr = (slot * incrementSize) + cpuStart.Ptr };

    public GpuDescriptorHandle GpuHandle => new() { Ptr = (slot * incrementSize) + gpuStart.Ptr };
}