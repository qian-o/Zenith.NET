using Silk.NET.Core.Native;
using Silk.NET.Direct3D12;

namespace Zenith.NET.DirectX12;

internal unsafe class DXDescriptorPool : GraphicsResource
{
    private const uint DescriptorCount = 512;

    private readonly bool[] descriptors = new bool[DescriptorCount];

    public uint IncrementSize;

    public ComPtr<ID3D12DescriptorHeap> Heap;

    public CpuDescriptorHandle Start;

    public DXDescriptorPool(GraphicsContext context, DescriptorHeapType type) : base(context)
    {
        IncrementSize = Context.Device.GetDescriptorHandleIncrementSize(type);

        DescriptorHeapDesc desc = new()
        {
            Type = type,
            NumDescriptors = DescriptorCount
        };

        Context.Device.CreateDescriptorHeap(&desc, out Heap).Success();

        Start = Heap.GetCPUDescriptorHandleForHeapStart();
    }

    public new DXGraphicsContext Context => (DXGraphicsContext)base.Context;

    public bool TryAllocate(out CpuDescriptorHandle handle)
    {
        handle = default;

        for (uint i = 0; i < DescriptorCount; i++)
        {
            if (!descriptors[i])
            {
                handle = new(Start.Ptr + (IncrementSize * i));

                return descriptors[i] = true;
            }
        }

        return false;
    }

    public void Free(CpuDescriptorHandle handle)
    {
        descriptors[(handle.Ptr - Start.Ptr) / IncrementSize] = false;
    }

    protected override void SetResourceName(string name)
    {
    }

    protected override void Destroy()
    {
        Heap.Dispose();
    }
}
