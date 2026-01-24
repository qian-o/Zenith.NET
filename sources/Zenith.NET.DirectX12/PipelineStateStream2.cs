using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using Silk.NET.Direct3D12;
using Silk.NET.DXGI;
using static Silk.NET.Direct3D12.RTFormatArray;

namespace Zenith.NET.DirectX12;

internal unsafe struct PipelineStateStream2()
{
    public StreamFlags Flags = new();

    public StreamNodeMask NodeMask = new();

    public StreamRootSignature RootSignature = new();

    public StreamInputLayout InputLayout = new();

    public StreamIBStripCutValue IBStripCutValue = new();

    public StreamPrimitiveTopology PrimitiveTopologyType = new();

    public StreamVS VS = new();

    public StreamGS GS = new();

    public StreamStreamOutput StreamOutput = new();

    public StreamHS HS = new();

    public StreamDS DS = new();

    public StreamPS PS = new();

    public StreamAS AS = new();

    public StreamMS MS = new();

    public StreamCS CS = new();

    public StreamBlend BlendState = new();

    public StreamDepthStencil1 DepthStencilState = new();

    public StreamDepthStencilFormat DSVFormat = new();

    public StreamRasterizer RasterizerState = new();

    public StreamRenderTargetFormats RTVFormats = new();

    public StreamSampleDesc SampleDesc = new();

    public StreamSampleMask SampleMask = new();

    public StreamCachedPso CachedPSO = new();

    public StreamViewInstancing ViewInstancingDesc = new();

    public static explicit operator PipelineStateStream2(GraphicsPipelineStateDesc desc)
    {
        return new()
        {
            Flags = { Data = desc.Flags },
            NodeMask = { Data = desc.NodeMask },
            RootSignature = { Data = (nint)desc.PRootSignature },
            InputLayout = { Data = desc.InputLayout },
            IBStripCutValue = { Data = desc.IBStripCutValue },
            PrimitiveTopologyType = { Data = desc.PrimitiveTopologyType },
            VS = { Data = desc.VS },
            GS = { Data = desc.GS },
            StreamOutput = { Data = desc.StreamOutput },
            HS = { Data = desc.HS },
            DS = { Data = desc.DS },
            PS = { Data = desc.PS },
            BlendState = { Data = desc.BlendState },
            DepthStencilState =
            {
                Data = new()
                {
                    DepthEnable = desc.DepthStencilState.DepthEnable,
                    DepthWriteMask = desc.DepthStencilState.DepthWriteMask,
                    DepthFunc = desc.DepthStencilState.DepthFunc,
                    StencilEnable = desc.DepthStencilState.StencilEnable,
                    StencilReadMask = desc.DepthStencilState.StencilReadMask,
                    StencilWriteMask = desc.DepthStencilState.StencilWriteMask,
                    FrontFace = desc.DepthStencilState.FrontFace,
                    BackFace = desc.DepthStencilState.BackFace
                }
            },
            DSVFormat = { Data = desc.DSVFormat },
            RasterizerState = { Data = desc.RasterizerState },
            RTVFormats =
            {
                Data = new()
                {
                    NumRenderTargets = desc.NumRenderTargets,
                    RTFormats = *(RTFormatsBuffer*)&desc.RTVFormats
                }
            },
            SampleDesc = { Data = desc.SampleDesc },
            SampleMask = { Data = desc.SampleMask },
            CachedPSO = { Data = desc.CachedPSO }
        };
    }

    internal struct SubObject<T>(PipelineStateSubobjectType type) where T : unmanaged
    {
        public readonly PipelineStateSubobjectType Type = type;

        public T Data;
    }

    [StructLayout(LayoutKind.Explicit)]
    internal struct StreamFlags()
    {
        [FieldOffset(0)]
        private readonly nint _padding;

        [FieldOffset(0)]
        public SubObject<PipelineStateFlags> Object = new(PipelineStateSubobjectType.Flags);

        [UnscopedRef]
        public ref PipelineStateFlags Data => ref Object.Data;
    }

    [StructLayout(LayoutKind.Explicit)]
    internal struct StreamNodeMask()
    {
        [FieldOffset(0)]
        private readonly nint _padding;

        [FieldOffset(0)]
        public SubObject<uint> Object = new(PipelineStateSubobjectType.NodeMask);

        [UnscopedRef]
        public ref uint Data => ref Object.Data;
    }

    [StructLayout(LayoutKind.Explicit)]
    internal struct StreamRootSignature()
    {
        [FieldOffset(0)]
        private readonly nint _padding;

        [FieldOffset(0)]
        public SubObject<nint> Object = new(PipelineStateSubobjectType.RootSignature);

        [UnscopedRef]
        public ref nint Data => ref Object.Data;
    }

    [StructLayout(LayoutKind.Explicit)]
    internal struct StreamInputLayout()
    {
        [FieldOffset(0)]
        private readonly nint _padding;

        [FieldOffset(0)]
        public SubObject<InputLayoutDesc> Object = new(PipelineStateSubobjectType.InputLayout);

        [UnscopedRef]
        public ref InputLayoutDesc Data => ref Object.Data;
    }

    [StructLayout(LayoutKind.Explicit)]
    internal struct StreamIBStripCutValue()
    {
        [FieldOffset(0)]
        private readonly nint _padding;

        [FieldOffset(0)]
        public SubObject<IndexBufferStripCutValue> Object = new(PipelineStateSubobjectType.IBStripCutValue);

        [UnscopedRef]
        public ref IndexBufferStripCutValue Data => ref Object.Data;
    }

    [StructLayout(LayoutKind.Explicit)]
    internal struct StreamPrimitiveTopology()
    {
        [FieldOffset(0)]
        private readonly nint _padding;

        [FieldOffset(0)]
        public SubObject<PrimitiveTopologyType> Object = new(PipelineStateSubobjectType.PrimitiveTopology);

        [UnscopedRef]
        public ref PrimitiveTopologyType Data => ref Object.Data;
    }

    [StructLayout(LayoutKind.Explicit)]
    internal struct StreamVS()
    {
        [FieldOffset(0)]
        private readonly nint _padding;

        [FieldOffset(0)]
        public SubObject<ShaderBytecode> Object = new(PipelineStateSubobjectType.VS);

        [UnscopedRef]
        public ref ShaderBytecode Data => ref Object.Data;
    }

    [StructLayout(LayoutKind.Explicit)]
    internal struct StreamGS()
    {
        [FieldOffset(0)]
        private readonly nint _padding;

        [FieldOffset(0)]
        public SubObject<ShaderBytecode> Object = new(PipelineStateSubobjectType.GS);

        [UnscopedRef]
        public ref ShaderBytecode Data => ref Object.Data;
    }

    [StructLayout(LayoutKind.Explicit)]
    internal struct StreamStreamOutput()
    {
        [FieldOffset(0)]
        private readonly nint _padding;

        [FieldOffset(0)]
        public SubObject<StreamOutputDesc> Object = new(PipelineStateSubobjectType.StreamOutput);

        [UnscopedRef]
        public ref StreamOutputDesc Data => ref Object.Data;
    }

    [StructLayout(LayoutKind.Explicit)]
    internal struct StreamHS()
    {
        [FieldOffset(0)]
        private readonly nint _padding;

        [FieldOffset(0)]
        public SubObject<ShaderBytecode> Object = new(PipelineStateSubobjectType.HS);

        [UnscopedRef]
        public ref ShaderBytecode Data => ref Object.Data;
    }

    [StructLayout(LayoutKind.Explicit)]
    internal struct StreamDS()
    {
        [FieldOffset(0)]
        private readonly nint _padding;

        [FieldOffset(0)]
        public SubObject<ShaderBytecode> Object = new(PipelineStateSubobjectType.DS);

        [UnscopedRef]
        public ref ShaderBytecode Data => ref Object.Data;
    }

    [StructLayout(LayoutKind.Explicit)]
    internal struct StreamPS()
    {
        [FieldOffset(0)]
        private readonly nint _padding;

        [FieldOffset(0)]
        public SubObject<ShaderBytecode> Object = new(PipelineStateSubobjectType.PS);

        [UnscopedRef]
        public ref ShaderBytecode Data => ref Object.Data;
    }

    [StructLayout(LayoutKind.Explicit)]
    internal struct StreamAS()
    {
        [FieldOffset(0)]
        private readonly nint _padding;

        [FieldOffset(0)]
        public SubObject<ShaderBytecode> Object = new(PipelineStateSubobjectType.As);

        [UnscopedRef]
        public ref ShaderBytecode Data => ref Object.Data;
    }

    [StructLayout(LayoutKind.Explicit)]
    internal struct StreamMS()
    {
        [FieldOffset(0)]
        private readonly nint _padding;

        [FieldOffset(0)]
        public SubObject<ShaderBytecode> Object = new(PipelineStateSubobjectType.MS);

        [UnscopedRef]
        public ref ShaderBytecode Data => ref Object.Data;
    }

    [StructLayout(LayoutKind.Explicit)]
    internal struct StreamCS()
    {
        [FieldOffset(0)]
        private readonly nint _padding;

        [FieldOffset(0)]
        public SubObject<ShaderBytecode> Object = new(PipelineStateSubobjectType.CS);

        [UnscopedRef]
        public ref ShaderBytecode Data => ref Object.Data;
    }

    [StructLayout(LayoutKind.Explicit)]
    internal struct StreamBlend()
    {
        [FieldOffset(0)]
        private readonly nint _padding;

        [FieldOffset(0)]
        public SubObject<BlendDesc> Object = new(PipelineStateSubobjectType.Blend);

        [UnscopedRef]
        public ref BlendDesc Data => ref Object.Data;
    }

    [StructLayout(LayoutKind.Explicit)]
    internal struct StreamDepthStencil1()
    {
        [FieldOffset(0)]
        private readonly nint _padding;

        [FieldOffset(0)]
        public SubObject<DepthStencilDesc1> Object = new(PipelineStateSubobjectType.DepthStencil1);

        [UnscopedRef]
        public ref DepthStencilDesc1 Data => ref Object.Data;
    }

    [StructLayout(LayoutKind.Explicit)]
    internal struct StreamDepthStencilFormat()
    {
        [FieldOffset(0)]
        private readonly nint _padding;

        [FieldOffset(0)]
        public SubObject<Format> Object = new(PipelineStateSubobjectType.DepthStencilFormat);

        [UnscopedRef]
        public ref Format Data => ref Object.Data;
    }

    [StructLayout(LayoutKind.Explicit)]
    internal struct StreamRasterizer()
    {
        [FieldOffset(0)]
        private readonly nint _padding;

        [FieldOffset(0)]
        public SubObject<RasterizerDesc> Object = new(PipelineStateSubobjectType.Rasterizer);

        [UnscopedRef]
        public ref RasterizerDesc Data => ref Object.Data;
    }

    [StructLayout(LayoutKind.Explicit)]
    internal struct StreamRenderTargetFormats()
    {
        [FieldOffset(0)]
        private readonly nint _padding;

        [FieldOffset(0)]
        public SubObject<RTFormatArray> Object = new(PipelineStateSubobjectType.RenderTargetFormats);

        [UnscopedRef]
        public ref RTFormatArray Data => ref Object.Data;
    }

    [StructLayout(LayoutKind.Explicit)]
    internal struct StreamSampleDesc()
    {
        [FieldOffset(0)]
        private readonly nint _padding;

        [FieldOffset(0)]
        public SubObject<SampleDesc> Object = new(PipelineStateSubobjectType.SampleDesc);

        [UnscopedRef]
        public ref SampleDesc Data => ref Object.Data;
    }

    [StructLayout(LayoutKind.Explicit)]
    internal struct StreamSampleMask()
    {
        [FieldOffset(0)]
        private readonly nint _padding;

        [FieldOffset(0)]
        public SubObject<uint> Object = new(PipelineStateSubobjectType.SampleMask);

        [UnscopedRef]
        public ref uint Data => ref Object.Data;
    }

    [StructLayout(LayoutKind.Explicit)]
    internal struct StreamCachedPso()
    {
        [FieldOffset(0)]
        private readonly nint _padding;

        [FieldOffset(0)]
        public SubObject<CachedPipelineState> Object = new(PipelineStateSubobjectType.CachedPso);

        [UnscopedRef]
        public ref CachedPipelineState Data => ref Object.Data;
    }

    [StructLayout(LayoutKind.Explicit)]
    internal struct StreamViewInstancing()
    {
        [FieldOffset(0)]
        private readonly nint _padding;

        [FieldOffset(0)]
        public SubObject<ViewInstancingDesc> Object = new(PipelineStateSubobjectType.ViewInstancing);

        [UnscopedRef]
        public ref ViewInstancingDesc Data => ref Object.Data;
    }
}
