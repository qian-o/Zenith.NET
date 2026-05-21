using Silk.NET.Core.Native;
using Silk.NET.Direct3D12;

namespace Zenith.NET.DirectX12;

internal unsafe class DXDescriptorHeap : DisposableObject
{
    private readonly Stack<uint> recycled = [];

    public ComPtr<ID3D12DescriptorHeap> Heap;

    private uint head;

    public DXDescriptorHeap(DXGraphicsContext context, DescriptorHeapType type, uint numDescriptors, bool shaderVisible)
    {
        DescriptorHeapDesc desc = new()
        {
            Type = type,
            NumDescriptors = NumDescriptors = numDescriptors,
            Flags = shaderVisible ? DescriptorHeapFlags.ShaderVisible : DescriptorHeapFlags.None
        };

        context.Device10.CreateDescriptorHeap(&desc, out Heap).Success();

        IncrementSize = context.Device10.GetDescriptorHandleIncrementSize(type);
        CpuStart = Heap.GetCPUDescriptorHandleForHeapStart();
        GpuStart = shaderVisible ? Heap.GetGPUDescriptorHandleForHeapStart() : default;
    }

    public uint NumDescriptors { get; }

    public uint IncrementSize { get; }

    public CpuDescriptorHandle CpuStart { get; }

    public GpuDescriptorHandle GpuStart { get; }

    public DXDescriptorToken Allocate()
    {
        if (!recycled.TryPop(out uint slot))
        {
            slot = head++;
        }

        return new(slot, IncrementSize, CpuStart, GpuStart);
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

