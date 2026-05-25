using Silk.NET.Core.Native;
using Silk.NET.Direct3D12;

namespace Zenith.NET.DirectX12;

internal unsafe class DXDescriptorHeap : DisposableObject
{
    private readonly Stack<uint> recycled = [];

    public ComPtr<ID3D12DescriptorHeap> Heap;

    public CpuDescriptorHandle CpuStartHandle;

    public GpuDescriptorHandle GpuStartHandle;

    public uint IncrementSize;

    private uint head;

    public DXDescriptorHeap(DXGraphicsContext context, DescriptorHeapType type, uint numDescriptors, bool shaderVisible)
    {
        DescriptorHeapDesc desc = new()
        {
            Type = type,
            NumDescriptors = numDescriptors,
            Flags = shaderVisible ? DescriptorHeapFlags.ShaderVisible : DescriptorHeapFlags.None
        };

        context.Device10.CreateDescriptorHeap(&desc, SilkMarshal.GuidPtrOf<ID3D12DescriptorHeap>(), (void**)Heap.GetAddressOf()).Success();

        CpuStartHandle = Heap.GetCPUDescriptorHandleForHeapStart();
        GpuStartHandle = shaderVisible ? Heap.GetGPUDescriptorHandleForHeapStart() : default;
        IncrementSize = context.Device10.GetDescriptorHandleIncrementSize(type);
    }

    public DXDescriptorToken Allocate()
    {
        if (!recycled.TryPop(out uint slot))
        {
            slot = head++;
        }

        return new(this, slot);
    }

    public void Free(DXDescriptorToken token)
    {
        recycled.Push(token.Slot);
    }

    protected override void Destroy()
    {
        Heap.Dispose();
    }
}

