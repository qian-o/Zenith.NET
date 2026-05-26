using Silk.NET.Core.Native;
using Silk.NET.Direct3D12;

namespace Zenith.NET.DirectX12;

internal unsafe class DXComputePipeline : ComputePipeline
{
    public ComPtr<ID3D12PipelineState> PipelineState;

    public DXComputePipeline(DXGraphicsContext context, ComputePipelineDesc desc) : base(context, desc)
    {
        using ZenithMarshal.Scope scope = new();

        PipelineStateStream2 pipelineStateStream = new()
        {
            PRootSignature = (nint)context.RootSignature.Handle,
            CS = desc.ComputeShader.DirectX12().GetShaderBytecode(scope)
        };

        PipelineStateStreamDesc pipelineStateStreamDesc = new()
        {
            SizeInBytes = (uint)sizeof(PipelineStateStream2),
            PPipelineStateSubobjectStream = &pipelineStateStream
        };

        context.Device14.CreatePipelineState(&pipelineStateStreamDesc, SilkMarshal.GuidPtrOf<ID3D12PipelineState>(), (void**)PipelineState.GetAddressOf()).Success();
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
