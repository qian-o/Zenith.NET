using Silk.NET.Core.Native;
using Silk.NET.Direct3D12;

namespace Zenith.NET.DirectX12;

internal unsafe class DXDescriptorPool : GraphicsResource
{
    private const uint DescriptorCount = 128;

    private readonly CpuDescriptorHandle startHandle;
    private readonly uint incrementSize;
    private readonly bool[] allocatedSlots;

    public ComPtr<ID3D12DescriptorHeap> Heap;

    public DXDescriptorPool(GraphicsContext context, DescriptorHeapType type, out CpuDescriptorHandle initialHandle) : base(context)
    {
        DescriptorHeapDesc desc = new()
        {
            Type = type,
            NumDescriptors = DescriptorCount
        };

        Context.Device.CreateDescriptorHeap(&desc, out Heap).Success();

        initialHandle = startHandle = Heap.GetCPUDescriptorHandleForHeapStart();
        incrementSize = Context.Device.GetDescriptorHandleIncrementSize(type);
        allocatedSlots = new bool[DescriptorCount];
        allocatedSlots[0] = true;
    }

    public new DXGraphicsContext Context => (DXGraphicsContext)base.Context;

    public bool TryAllocate(out CpuDescriptorHandle handle)
    {
        handle = default;

        for (uint i = 0; i < DescriptorCount; i++)
        {
            if (!allocatedSlots[i])
            {
                handle = new(startHandle.Ptr + (incrementSize * i));

                return allocatedSlots[i] = true;
            }
        }

        return false;
    }

    public void Free(CpuDescriptorHandle handle)
    {
        allocatedSlots[(handle.Ptr - startHandle.Ptr) / incrementSize] = false;
    }

    protected override void SetResourceName(string name)
    {
    }

    protected override void Destroy()
    {
        Heap.Dispose();
    }
}
