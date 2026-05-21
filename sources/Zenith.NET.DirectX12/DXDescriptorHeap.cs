using Silk.NET.Core.Native;
using Silk.NET.Direct3D12;

namespace Zenith.NET.DirectX12;

internal unsafe class DXDescriptorHeap : DisposableObject
{
    private readonly Stack<uint> slots = [];

    public ComPtr<ID3D12DescriptorHeap> Heap;

    private uint currentIndex;

    public DXDescriptorHeap(DXGraphicsContext context, DescriptorHeapType type, uint capacity, bool shaderVisible)
    {
        DescriptorHeapDesc desc = new()
        {
            Type = type,
            NumDescriptors = Capacity = capacity,
            Flags = shaderVisible ? DescriptorHeapFlags.ShaderVisible : DescriptorHeapFlags.None
        };

        context.Device10.CreateDescriptorHeap(&desc, out Heap).Success();

        DescriptorSize = context.Device10.GetDescriptorHandleIncrementSize(type);
        CpuStart = Heap.GetCPUDescriptorHandleForHeapStart();
        GpuStart = shaderVisible ? Heap.GetGPUDescriptorHandleForHeapStart() : default;
    }

    public uint Capacity { get; }

    public uint DescriptorSize { get; }

    public CpuDescriptorHandle CpuStart { get; }

    public GpuDescriptorHandle GpuStart { get; }

    public DXDescriptorToken Allocate()
    {
        uint slot = slots.TryPop(out uint reused) ? reused : currentIndex++;

        return new(slot, DescriptorSize, CpuStart, GpuStart);
    }

    public void Free(DXDescriptorToken token)
    {
        slots.Push(token.Slot);
    }

    protected override void Destroy()
    {
        Heap.Dispose();
    }
}

