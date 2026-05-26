using Silk.NET.Core.Native;
using Silk.NET.Direct3D12;

namespace Zenith.NET.DirectX12;

internal class DXGraphicsPipeline : GraphicsPipeline
{
    public ComPtr<ID3D12PipelineState> PipelineState;

    public DXGraphicsPipeline(DXGraphicsContext context, GraphicsPipelineDesc desc) : base(context, desc)
    {
        using ZenithMarshal.Scope scope = new();

        GraphicsPipelineStateDesc graphicsPipelineStateDesc = new()
        {
            PRootSignature = context.RootSignature,
            VS = desc.VertexShader.DirectX12().GetShaderBytecode(scope),
            PS = desc.FragmentShader.DirectX12().GetShaderBytecode(scope),
            SampleMask = uint.MaxValue,
            PrimitiveTopologyType = DXFormats.DirectX12(desc.PrimitiveTopology)
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
