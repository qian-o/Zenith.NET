using Silk.NET.Core.Native;
using Silk.NET.Direct3D12;

namespace Zenith.NET.DirectX12;

internal unsafe class DXComputePipeline : ComputePipeline
{
    public ComPtr<ID3D12PipelineState> PipelineState;

    public DXComputePipeline(DXGraphicsContext context, ComputePipelineDesc desc) : base(context, desc)
    {
        using ZenithMarshal.Scope scope = new();

        ComputePipelineStateDesc computePipelineStateDesc = new()
        {
            PRootSignature = context.RootSignature,
            CS = desc.ComputeShader.DirectX12().GetShaderBytecode(scope)
        };

        context.Device10.CreateComputePipelineState(&computePipelineStateDesc, SilkMarshal.GuidPtrOf<ID3D12PipelineState>(), (void**)PipelineState.GetAddressOf()).Success();
    }

    public override nint GetNativeObject(NativeObjectType type)
    {
        return 0;
    }

    protected override void SetResourceName(string name)
    {
        PipelineState.SetName(name).Success();
    }

    protected override void Destroy()
    {
        PipelineState.Dispose();
    }
}
