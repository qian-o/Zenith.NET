using Silk.NET.Core.Native;
using Silk.NET.Direct3D12;

namespace Zenith.NET.DirectX12;

internal unsafe class DXHeap : GraphicsResource
{
    public ComPtr<ID3D12Heap> Heap;

    public DXHeap(DXGraphicsContext context, ResourceDesc resourceDesc, HeapType type, HeapFlags flags) : base(context)
    {
        ResourceAllocationInfo allocationInfo = context.Device.GetResourceAllocationInfo(0, 1, ref resourceDesc);

        HeapDesc desc = new()
        {
            SizeInBytes = allocationInfo.SizeInBytes,
            Properties = new(type),
            Alignment = allocationInfo.Alignment,
            Flags = flags
        };

        context.Device.CreateHeap(&desc, out Heap).Success();
    }

    protected override void SetResourceName(string name)
    {
    }

    protected override void Destroy()
    {
        Heap.Dispose();
    }
}
