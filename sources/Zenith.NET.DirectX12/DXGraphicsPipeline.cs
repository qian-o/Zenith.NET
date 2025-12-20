using Silk.NET.Core.Native;
using Silk.NET.Direct3D12;

namespace Zenith.NET.DirectX12;

internal class DXGraphicsPipeline : GraphicsPipeline
{
    public ComPtr<ID3D12RootSignature> RootSignature;

    public ComPtr<ID3D12PipelineState> PipelineState;

    public DXGraphicsPipeline(DXGraphicsContext context, GraphicsPipelineDesc desc) : base(context, desc)
    {
        using ZenithMarshal.Scope scope = new();

    }

    protected override void SetResourceName(string name)
    {
        PipelineState.SetName(name);
    }

    protected override void Destroy()
    {
        RootSignature.Dispose();
        PipelineState.Dispose();
    }
}
