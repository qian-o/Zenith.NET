using Silk.NET.Core.Native;
using Silk.NET.Direct3D12;

namespace Zenith.NET.DirectX12;

internal class DXMeshShadingPipeline : MeshShadingPipeline
{
    public ComPtr<ID3D12PipelineState> PipelineState;

    public DXMeshShadingPipeline(DXGraphicsContext context, MeshShadingPipelineDesc desc) : base(context, desc)
    {
        using ZenithMarshal.Scope scope = new();

        GraphicsPipelineStateDesc graphicsPipelineStateDesc = new()
        {
            PRootSignature = context.RootSignature,
            SampleMask = uint.MaxValue
        };
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
