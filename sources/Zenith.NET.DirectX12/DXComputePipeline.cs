using Silk.NET.Core.Native;
using Silk.NET.Direct3D12;

namespace Zenith.NET.DirectX12;

internal unsafe class DXComputePipeline : ComputePipeline
{
    public ComPtr<ID3D12RootSignature> RootSignature;

    public ComPtr<ID3D12PipelineState> PipelineState;

    public DXComputePipeline(DXGraphicsContext context, ComputePipelineDesc desc) : base(context, desc)
    {
        using ZenithMarshal.Scope scope = new();

        ComputePipelineStateDesc computePipelineStateDesc = new()
        {
            PRootSignature = RootSignature = desc.ResourceBindings.RootSignature(context),
            CS = desc.Compute.DirectX12().GetShaderBytecode(scope)
        };

        context.Device.CreateComputePipelineState(&computePipelineStateDesc, out PipelineState).Success();
    }

    protected override void SetResourceName(string name)
    {
        PipelineState.SetName(name).Success();
    }

    protected override void Destroy()
    {
        PipelineState.Dispose();
        RootSignature.Dispose();
    }
}
