using Silk.NET.Core.Native;
using Silk.NET.Direct3D12;

namespace Zenith.NET.DirectX12;

internal unsafe class DXDescriptorTable : GraphicsResource
{
    public ComPtr<ID3D12DescriptorHeap> Heap;

    private uint currentIndex;

    public DXDescriptorTable(DXGraphicsContext context, DescriptorHeapType type, uint count) : base(context)
    {
        DescriptorHeapDesc desc = new()
        {
            Type = Type = type,
            NumDescriptors = count,
            Flags = DescriptorHeapFlags.ShaderVisible
        };

        context.Device.CreateDescriptorHeap(&desc, out Heap).Success();

        DescriptorSize = context.Device.GetDescriptorHandleIncrementSize(type);
    }

    public new DXGraphicsContext Context => (DXGraphicsContext)base.Context;

    public DescriptorHeapType Type { get; }

    public uint DescriptorSize { get; }

    public GpuDescriptorHandle GpuCurrentHandle => new(Heap.GetGPUDescriptorHandleForHeapStart().Ptr + (DescriptorSize * currentIndex));

    public void Write(DXDescriptorToken token)
    {
        Context.Device.CopyDescriptorsSimple(token.Length, new(Heap.GetCPUDescriptorHandleForHeapStart().Ptr + (DescriptorSize * currentIndex)), token.Handle, Type);

        currentIndex += token.Length;
    }

    public void Reset()
    {
        currentIndex = 0;
    }

    protected override void SetResourceName(string name)
    {
    }

    protected override void Destroy()
    {
        Heap.Dispose();
    }
}
