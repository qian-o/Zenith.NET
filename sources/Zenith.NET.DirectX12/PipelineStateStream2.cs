using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using Silk.NET.Direct3D12;
using Silk.NET.DXGI;

namespace Zenith.NET.DirectX12;

internal struct PipelineStateStream2()
{
    private StreamFlags _flags = new();
    private StreamNodeMask _nodeMask = new();
    private StreamRootSignature _pRootSignature = new();
    private StreamInputLayout _inputLayout = new();
    private StreamIBStripCutValue _ibStripCutValue = new();
    private StreamPrimitiveTopology _primitiveTopologyType = new();
    private StreamVS _vs = new();
    private StreamGS _gs = new();
    private StreamStreamOutput _streamOutput = new();
    private StreamHS _hs = new();
    private StreamDS _ds = new();
    private StreamPS _ps = new();
    private StreamAS _as = new();
    private StreamMS _ms = new();
    private StreamCS _cs = new();
    private StreamBlend _blendState = new();
    private StreamDepthStencil1 _depthStencilState = new();
    private StreamDepthStencilFormat _dsvFormat = new();
    private StreamRasterizer _rasterizerState = new();
    private StreamRenderTargetFormats _rtvFormats = new();
    private StreamSampleDesc _sampleDesc = new();
    private StreamSampleMask _sampleMask = new();
    private StreamCachedPso _cachedPSO = new();
    private StreamViewInstancing _viewInstancingDesc = new();

    [UnscopedRef]
    public ref PipelineStateFlags Flags => ref _flags.Data;

    [UnscopedRef]
    public ref uint NodeMask => ref _nodeMask.Data;

    [UnscopedRef]
    public ref nint PRootSignature => ref _pRootSignature.Data;

    [UnscopedRef]
    public ref InputLayoutDesc InputLayout => ref _inputLayout.Data;

    [UnscopedRef]
    public ref IndexBufferStripCutValue IBStripCutValue => ref _ibStripCutValue.Data;

    [UnscopedRef]
    public ref PrimitiveTopologyType PrimitiveTopologyType => ref _primitiveTopologyType.Data;

    [UnscopedRef]
    public ref ShaderBytecode VS => ref _vs.Data;

    [UnscopedRef]
    public ref ShaderBytecode GS => ref _gs.Data;

    [UnscopedRef]
    public ref StreamOutputDesc StreamOutput => ref _streamOutput.Data;

    [UnscopedRef]
    public ref ShaderBytecode HS => ref _hs.Data;

    [UnscopedRef]
    public ref ShaderBytecode DS => ref _ds.Data;

    [UnscopedRef]
    public ref ShaderBytecode PS => ref _ps.Data;

    [UnscopedRef]
    public ref ShaderBytecode AS => ref _as.Data;

    [UnscopedRef]
    public ref ShaderBytecode MS => ref _ms.Data;

    [UnscopedRef]
    public ref ShaderBytecode CS => ref _cs.Data;

    [UnscopedRef]
    public ref BlendDesc BlendState => ref _blendState.Data;

    [UnscopedRef]
    public ref DepthStencilDesc1 DepthStencilState => ref _depthStencilState.Data;

    [UnscopedRef]
    public ref Format DSVFormat => ref _dsvFormat.Data;

    [UnscopedRef]
    public ref RasterizerDesc RasterizerState => ref _rasterizerState.Data;

    [UnscopedRef]
    public ref RTFormatArray RTVFormats => ref _rtvFormats.Data;

    [UnscopedRef]
    public ref SampleDesc SampleDesc => ref _sampleDesc.Data;

    [UnscopedRef]
    public ref uint SampleMask => ref _sampleMask.Data;

    [UnscopedRef]
    public ref CachedPipelineState CachedPSO => ref _cachedPSO.Data;

    [UnscopedRef]
    public ref ViewInstancingDesc ViewInstancingDesc => ref _viewInstancingDesc.Data;

    private struct SubObject<T>(PipelineStateSubobjectType type) where T : unmanaged
    {
        public readonly PipelineStateSubobjectType Type = type;

        public T Data;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct StreamFlags()
    {
        [FieldOffset(0)]
        private readonly nint _padding;

        [FieldOffset(0)]
        public SubObject<PipelineStateFlags> Object = new(PipelineStateSubobjectType.Flags);

        [UnscopedRef]
        public ref PipelineStateFlags Data => ref Object.Data;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct StreamNodeMask()
    {
        [FieldOffset(0)]
        private readonly nint _padding;

        [FieldOffset(0)]
        public SubObject<uint> Object = new(PipelineStateSubobjectType.NodeMask);

        [UnscopedRef]
        public ref uint Data => ref Object.Data;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct StreamRootSignature()
    {
        [FieldOffset(0)]
        private readonly nint _padding;

        [FieldOffset(0)]
        public SubObject<nint> Object = new(PipelineStateSubobjectType.RootSignature);

        [UnscopedRef]
        public ref nint Data => ref Object.Data;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct StreamInputLayout()
    {
        [FieldOffset(0)]
        private readonly nint _padding;

        [FieldOffset(0)]
        public SubObject<InputLayoutDesc> Object = new(PipelineStateSubobjectType.InputLayout);

        [UnscopedRef]
        public ref InputLayoutDesc Data => ref Object.Data;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct StreamIBStripCutValue()
    {
        [FieldOffset(0)]
        private readonly nint _padding;

        [FieldOffset(0)]
        public SubObject<IndexBufferStripCutValue> Object = new(PipelineStateSubobjectType.IBStripCutValue);

        [UnscopedRef]
        public ref IndexBufferStripCutValue Data => ref Object.Data;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct StreamPrimitiveTopology()
    {
        [FieldOffset(0)]
        private readonly nint _padding;

        [FieldOffset(0)]
        public SubObject<PrimitiveTopologyType> Object = new(PipelineStateSubobjectType.PrimitiveTopology);

        [UnscopedRef]
        public ref PrimitiveTopologyType Data => ref Object.Data;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct StreamVS()
    {
        [FieldOffset(0)]
        private readonly nint _padding;

        [FieldOffset(0)]
        public SubObject<ShaderBytecode> Object = new(PipelineStateSubobjectType.VS);

        [UnscopedRef]
        public ref ShaderBytecode Data => ref Object.Data;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct StreamGS()
    {
        [FieldOffset(0)]
        private readonly nint _padding;

        [FieldOffset(0)]
        public SubObject<ShaderBytecode> Object = new(PipelineStateSubobjectType.GS);

        [UnscopedRef]
        public ref ShaderBytecode Data => ref Object.Data;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct StreamStreamOutput()
    {
        [FieldOffset(0)]
        private readonly nint _padding;

        [FieldOffset(0)]
        public SubObject<StreamOutputDesc> Object = new(PipelineStateSubobjectType.StreamOutput);

        [UnscopedRef]
        public ref StreamOutputDesc Data => ref Object.Data;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct StreamHS()
    {
        [FieldOffset(0)]
        private readonly nint _padding;

        [FieldOffset(0)]
        public SubObject<ShaderBytecode> Object = new(PipelineStateSubobjectType.HS);

        [UnscopedRef]
        public ref ShaderBytecode Data => ref Object.Data;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct StreamDS()
    {
        [FieldOffset(0)]
        private readonly nint _padding;

        [FieldOffset(0)]
        public SubObject<ShaderBytecode> Object = new(PipelineStateSubobjectType.DS);

        [UnscopedRef]
        public ref ShaderBytecode Data => ref Object.Data;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct StreamPS()
    {
        [FieldOffset(0)]
        private readonly nint _padding;

        [FieldOffset(0)]
        public SubObject<ShaderBytecode> Object = new(PipelineStateSubobjectType.PS);

        [UnscopedRef]
        public ref ShaderBytecode Data => ref Object.Data;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct StreamAS()
    {
        [FieldOffset(0)]
        private readonly nint _padding;

        [FieldOffset(0)]
        public SubObject<ShaderBytecode> Object = new(PipelineStateSubobjectType.As);

        [UnscopedRef]
        public ref ShaderBytecode Data => ref Object.Data;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct StreamMS()
    {
        [FieldOffset(0)]
        private readonly nint _padding;

        [FieldOffset(0)]
        public SubObject<ShaderBytecode> Object = new(PipelineStateSubobjectType.MS);

        [UnscopedRef]
        public ref ShaderBytecode Data => ref Object.Data;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct StreamCS()
    {
        [FieldOffset(0)]
        private readonly nint _padding;

        [FieldOffset(0)]
        public SubObject<ShaderBytecode> Object = new(PipelineStateSubobjectType.CS);

        [UnscopedRef]
        public ref ShaderBytecode Data => ref Object.Data;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct StreamBlend()
    {
        [FieldOffset(0)]
        private readonly nint _padding;

        [FieldOffset(0)]
        public SubObject<BlendDesc> Object = new(PipelineStateSubobjectType.Blend);

        [UnscopedRef]
        public ref BlendDesc Data => ref Object.Data;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct StreamDepthStencil1()
    {
        [FieldOffset(0)]
        private readonly nint _padding;

        [FieldOffset(0)]
        public SubObject<DepthStencilDesc1> Object = new(PipelineStateSubobjectType.DepthStencil1);

        [UnscopedRef]
        public ref DepthStencilDesc1 Data => ref Object.Data;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct StreamDepthStencilFormat()
    {
        [FieldOffset(0)]
        private readonly nint _padding;

        [FieldOffset(0)]
        public SubObject<Format> Object = new(PipelineStateSubobjectType.DepthStencilFormat);

        [UnscopedRef]
        public ref Format Data => ref Object.Data;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct StreamRasterizer()
    {
        [FieldOffset(0)]
        private readonly nint _padding;

        [FieldOffset(0)]
        public SubObject<RasterizerDesc> Object = new(PipelineStateSubobjectType.Rasterizer);

        [UnscopedRef]
        public ref RasterizerDesc Data => ref Object.Data;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct StreamRenderTargetFormats()
    {
        [FieldOffset(0)]
        private readonly nint _padding;

        [FieldOffset(0)]
        public SubObject<RTFormatArray> Object = new(PipelineStateSubobjectType.RenderTargetFormats);

        [UnscopedRef]
        public ref RTFormatArray Data => ref Object.Data;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct StreamSampleDesc()
    {
        [FieldOffset(0)]
        private readonly nint _padding;

        [FieldOffset(0)]
        public SubObject<SampleDesc> Object = new(PipelineStateSubobjectType.SampleDesc);

        [UnscopedRef]
        public ref SampleDesc Data => ref Object.Data;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct StreamSampleMask()
    {
        [FieldOffset(0)]
        private readonly nint _padding;

        [FieldOffset(0)]
        public SubObject<uint> Object = new(PipelineStateSubobjectType.SampleMask);

        [UnscopedRef]
        public ref uint Data => ref Object.Data;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct StreamCachedPso()
    {
        [FieldOffset(0)]
        private readonly nint _padding;

        [FieldOffset(0)]
        public SubObject<CachedPipelineState> Object = new(PipelineStateSubobjectType.CachedPso);

        [UnscopedRef]
        public ref CachedPipelineState Data => ref Object.Data;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct StreamViewInstancing()
    {
        [FieldOffset(0)]
        private readonly nint _padding;

        [FieldOffset(0)]
        public SubObject<ViewInstancingDesc> Object = new(PipelineStateSubobjectType.ViewInstancing);

        [UnscopedRef]
        public ref ViewInstancingDesc Data => ref Object.Data;
    }
}