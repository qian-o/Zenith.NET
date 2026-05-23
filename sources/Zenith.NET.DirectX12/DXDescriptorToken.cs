using Silk.NET.Direct3D12;

namespace Zenith.NET.DirectX12;

internal readonly struct DXDescriptorToken(DXDescriptorHeap heap, uint slot) : IDisposable
{
    public readonly uint Slot = slot;

    public readonly ResourceHandle ResourceHandle = new(slot, 0);

    public readonly CpuDescriptorHandle CpuHandle = new() { Ptr = (slot * heap.IncrementSize) + heap.CpuStartHandle.Ptr };

    public readonly GpuDescriptorHandle GpuHandle = new() { Ptr = (slot * heap.IncrementSize) + heap.GpuStartHandle.Ptr };

    public void Dispose()
    {
        heap.Free(this);
    }
}