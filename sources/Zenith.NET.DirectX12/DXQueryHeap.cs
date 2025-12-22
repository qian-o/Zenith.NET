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

        context.Device.CreateQueryHeap(&queryHeapDesc, out QueryHeap).Success();
    }

    protected override void GetResultsImpl(Span<ulong> results, uint startIndex)
    {
        throw new NotImplementedException();
    }

    protected override void SetResourceName(string name)
    {
        QueryHeap.SetName(name).Success();
    }

    protected override void Destroy()
    {
        QueryHeap.Dispose();
    }
}
