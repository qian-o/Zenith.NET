using Silk.NET.Core.Native;
using Silk.NET.Direct3D12;

namespace Zenith.NET.DirectX12;

internal unsafe class DXDescriptorPool : GraphicsResource
{
    private const uint DescriptorCount = 256;

    private readonly bool[] slots = new bool[DescriptorCount];
    private readonly uint handleSize;
    private readonly CpuDescriptorHandle startHandle;

    public ComPtr<ID3D12DescriptorHeap> Heap;

    public DXDescriptorPool(DXGraphicsContext context, DescriptorHeapType type, out CpuDescriptorHandle initialHandle) : base(context)
    {
        slots[0] = true;

        DescriptorHeapDesc desc = new()
        {
            Type = type,
            NumDescriptors = DescriptorCount
        };

        context.Device.CreateDescriptorHeap(&desc, out Heap).Success();

        handleSize = context.Device.GetDescriptorHandleIncrementSize(type);
        initialHandle = startHandle = Heap.GetCPUDescriptorHandleForHeapStart();
    }

    public bool TryAllocate(out CpuDescriptorHandle handle)
    {
        handle = default;

        for (uint i = 0; i < DescriptorCount; i++)
        {
            if (!slots[i])
            {
                handle = new(startHandle.Ptr + (handleSize * i));

                return slots[i] = true;
            }
        }

        return false;
    }

    public void Free(CpuDescriptorHandle handle)
    {
        slots[(handle.Ptr - startHandle.Ptr) / handleSize] = false;
    }

    protected override void SetResourceName(string name)
    {
    }

    protected override void Destroy()
    {
        Heap.Dispose();
    }
}
