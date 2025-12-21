using Silk.NET.Core.Native;
using Silk.NET.Direct3D12;

namespace Zenith.NET.DirectX12;

internal unsafe class DXDescriptorPool : GraphicsResource
{
    private const uint DescriptorCount = 512;

    private readonly bool[] slots = new bool[DescriptorCount];

    public ComPtr<ID3D12DescriptorHeap> Heap;

    public DXDescriptorPool(DXGraphicsContext context, DescriptorHeapType type) : base(context)
    {
        DescriptorHeapDesc desc = new()
        {
            Type = type,
            NumDescriptors = DescriptorCount
        };

        context.Device.CreateDescriptorHeap(&desc, out Heap).Success();

        StartHandle = Heap.GetCPUDescriptorHandleForHeapStart();
        HandleSize = context.Device.GetDescriptorHandleIncrementSize(type);
    }

    public CpuDescriptorHandle StartHandle { get; }

    public uint HandleSize { get; }

    public bool TryAllocate(uint length, out CpuDescriptorHandle handle)
    {
        handle = default;

        for (uint i = 0; i <= DescriptorCount - length; i++)
        {
            bool available = true;

            for (uint j = 0; j < length; j++)
            {
                if (slots[i + j])
                {
                    available = false;

                    break;
                }
            }

            if (available)
            {
                for (uint j = 0; j < length; j++)
                {
                    slots[i + j] = true;
                }

                handle = new(StartHandle.Ptr + (HandleSize * i));

                return true;
            }
        }

        return false;
    }

    public void Free(CpuDescriptorHandle handle, uint length)
    {
        for (uint i = (uint)((handle.Ptr - StartHandle.Ptr) / HandleSize); i < length; i++)
        {
            slots[i] = false;
        }
    }

    protected override void SetResourceName(string name)
    {
    }

    protected override void Destroy()
    {
        Heap.Dispose();
    }
}
