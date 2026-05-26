using Silk.NET.Core.Native;
using Silk.NET.Direct3D12;

namespace Zenith.NET.DirectX12;

internal unsafe class DXQueryHeap : QueryHeap
{
    public ComPtr<ID3D12QueryHeap> QueryHeap;

    public DXQueryHeap(DXGraphicsContext context, QueryHeapDesc desc) : base(context, desc)
    {
        DxQueryHeapDesc queryHeapDesc = new()
        {
            Type = DXFormats.DirectX12(desc.Type),
            Count = desc.Count
        };

        context.Device10.CreateQueryHeap(&queryHeapDesc, SilkMarshal.GuidPtrOf<ID3D12QueryHeap>(), (void**)QueryHeap.GetAddressOf()).Success();

        Buffer = new(context, new() { SizeInBytes = sizeof(ulong) * desc.Count, Access = BufferAccess.CpuReadOnly }, null);
    }

    public DXBuffer Buffer { get; }

    public override nint GetNativeObject(NativeObjectType type)
    {
        return 0;
    }

    protected override void GetResultsImpl(Span<ulong> results, uint startIndex)
    {
        MappedMemory mappedMemory = Buffer.Map();

        new Span<ulong>((void*)(mappedMemory.Pointer + (sizeof(ulong) * startIndex)), results.Length).CopyTo(results);

        Buffer.Unmap();
    }

    protected override void SetResourceName(string name)
    {
        QueryHeap.SetName(name).Success();
    }

    protected override void Destroy()
    {
        Buffer.Dispose();

        QueryHeap.Dispose();
    }
}
