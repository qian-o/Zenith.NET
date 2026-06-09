using Silk.NET.Direct3D12;

namespace Zenith.NET.DirectX12;

internal readonly struct DXDescriptorToken(DXDescriptorHeap heap, uint slot) : IDisposable
{
    public readonly uint Slot = slot;

    public readonly ResourceHandle ResourceHandle = new(slot, 0);

    public readonly CpuDescriptorHandle CpuHandle = new()
    {
        Ptr = heap.CpuStartHandle.Ptr + (slot * heap.IncrementSize)
    };

    public readonly GpuDescriptorHandle GpuHandle = new()
    {
        Ptr = heap.GpuStartHandle.Ptr + (slot * heap.IncrementSize)
    };

    public void Dispose()
    {
        heap.Free(this);
    }
}