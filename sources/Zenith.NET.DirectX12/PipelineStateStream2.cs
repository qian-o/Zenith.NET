using System.Runtime.InteropServices;
using Silk.NET.Direct3D12;
using Silk.NET.DXGI;
using static Silk.NET.Direct3D12.RTFormatArray;

namespace Zenith.NET.DirectX12;

internal unsafe struct PipelineStateStream2()
{
    public SubObject<PipelineStateFlags> Flags = new(PipelineStateSubobjectType.Flags);

    public SubObject<uint> NodeMask = new(PipelineStateSubobjectType.NodeMask);

    public SubObject<nint> RootSignature = new(PipelineStateSubobjectType.RootSignature);

    public SubObject<InputLayoutDesc> InputLayout = new(PipelineStateSubobjectType.InputLayout);

    public SubObject<IndexBufferStripCutValue> IBStripCutValue = new(PipelineStateSubobjectType.IBStripCutValue);

    public SubObject<PrimitiveTopologyType> PrimitiveTopology = new(PipelineStateSubobjectType.PrimitiveTopology);

    public SubObject<ShaderBytecode> VS = new(PipelineStateSubobjectType.VS);

    public SubObject<ShaderBytecode> GS = new(PipelineStateSubobjectType.GS);

    public SubObject<StreamOutputDesc> StreamOutput = new(PipelineStateSubobjectType.StreamOutput);

    public SubObject<ShaderBytecode> HS = new(PipelineStateSubobjectType.HS);

    public SubObject<ShaderBytecode> DS = new(PipelineStateSubobjectType.DS);

    public SubObject<ShaderBytecode> PS = new(PipelineStateSubobjectType.PS);

    public SubObject<ShaderBytecode> AS = new(PipelineStateSubobjectType.As);

    public SubObject<ShaderBytecode> MS = new(PipelineStateSubobjectType.MS);

    public SubObject<ShaderBytecode> CS = new(PipelineStateSubobjectType.CS);

    public SubObject<BlendDesc> Blend = new(PipelineStateSubobjectType.Blend);

    public SubObject<DepthStencilDesc1> DepthStencil1 = new(PipelineStateSubobjectType.DepthStencil1);

    public SubObject<Format> DepthStencilFormat = new(PipelineStateSubobjectType.DepthStencilFormat);

    public SubObject<RasterizerDesc> Rasterizer = new(PipelineStateSubobjectType.Rasterizer);

    public SubObject<RTFormatArray> RenderTargetFormats = new(PipelineStateSubobjectType.RenderTargetFormats);

    public SubObject<SampleDesc> SampleDesc = new(PipelineStateSubobjectType.SampleDesc);

    public SubObject<uint> SampleMask = new(PipelineStateSubobjectType.SampleMask);

    public SubObject<CachedPipelineState> CachedPso = new(PipelineStateSubobjectType.CachedPso);

    public SubObject<ViewInstancingDesc> ViewInstancing = new(PipelineStateSubobjectType.ViewInstancing);

    public static explicit operator PipelineStateStream2(GraphicsPipelineStateDesc desc)
    {
        PipelineStateStream2 pipelineStateStream2 = new();

        pipelineStateStream2.Flags.Data = desc.Flags;
        pipelineStateStream2.NodeMask.Data = desc.NodeMask;
        pipelineStateStream2.RootSignature.Data = (nint)desc.PRootSignature;
        pipelineStateStream2.InputLayout.Data = desc.InputLayout;
        pipelineStateStream2.IBStripCutValue.Data = desc.IBStripCutValue;
        pipelineStateStream2.PrimitiveTopology.Data = desc.PrimitiveTopologyType;
        pipelineStateStream2.VS.Data = desc.VS;
        pipelineStateStream2.GS.Data = desc.GS;
        pipelineStateStream2.StreamOutput.Data = desc.StreamOutput;
        pipelineStateStream2.HS.Data = desc.HS;
        pipelineStateStream2.DS.Data = desc.DS;
        pipelineStateStream2.PS.Data = desc.PS;
        pipelineStateStream2.Blend.Data = desc.BlendState;
        pipelineStateStream2.DepthStencil1.Data = new()
        {
            DepthEnable = desc.DepthStencilState.DepthEnable,
            DepthWriteMask = desc.DepthStencilState.DepthWriteMask,
            DepthFunc = desc.DepthStencilState.DepthFunc,
            StencilEnable = desc.DepthStencilState.StencilEnable,
            StencilReadMask = desc.DepthStencilState.StencilReadMask,
            StencilWriteMask = desc.DepthStencilState.StencilWriteMask,
            FrontFace = desc.DepthStencilState.FrontFace,
            BackFace = desc.DepthStencilState.BackFace
        };
        pipelineStateStream2.DepthStencilFormat.Data = desc.DSVFormat;
        pipelineStateStream2.Rasterizer.Data = desc.RasterizerState;
        pipelineStateStream2.RenderTargetFormats.Data = new()
        {
            NumRenderTargets = desc.NumRenderTargets,
            RTFormats = *(RTFormatsBuffer*)&desc.RTVFormats
        };
        pipelineStateStream2.SampleDesc.Data = desc.SampleDesc;
        pipelineStateStream2.SampleMask.Data = desc.SampleMask;
        pipelineStateStream2.CachedPso.Data = desc.CachedPSO;

        return pipelineStateStream2;
    }

    [StructLayout(LayoutKind.Explicit)]
    internal struct SubObject<T>(PipelineStateSubobjectType type) where T : unmanaged
    {
        [FieldOffset(0)]
        private readonly nint padding;

        [FieldOffset(0)]
        public readonly PipelineStateSubobjectType Type = type;

        [FieldOffset(4)]
        public T Data;
    }
}
