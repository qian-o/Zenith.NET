using Silk.NET.Core.Native;
using Silk.NET.Direct3D12;

namespace Zenith.NET.DirectX12;

internal unsafe class DXMeshShadingPipeline : MeshShadingPipeline
{
    public ComPtr<ID3D12PipelineState> PipelineState;

    public DXMeshShadingPipeline(DXGraphicsContext context, MeshShadingPipelineDesc desc) : base(context, desc)
    {
        using ZenithMarshal.Scope scope = new();

        PipelineStateStream2 pipelineStateStream = new()
        {
            PRootSignature = (nint)context.RootSignature.Handle,
            PrimitiveTopologyType = DXFormats.DirectX12(desc.PrimitiveTopology).TopologyType,
            PS = desc.FragmentShader.DirectX12().GetShaderBytecode(scope),
            AS = (desc.TaskShader?.DirectX12().GetShaderBytecode(scope)) ?? default,
            MS = desc.MeshShader.DirectX12().GetShaderBytecode(scope),
            SampleMask = uint.MaxValue
        };

        // AttachmentFormats
        {
            pipelineStateStream.DSVFormat = DXFormats.DirectX12(desc.AttachmentFormats.DepthStencilFormat ?? PixelFormat.Unknown);

            pipelineStateStream.RTVFormats.NumRenderTargets = (uint)desc.AttachmentFormats.ColorFormats.Length;

            for (int i = 0; i < desc.AttachmentFormats.ColorFormats.Length; i++)
            {
                pipelineStateStream.RTVFormats.RTFormats[i] = DXFormats.DirectX12(desc.AttachmentFormats.ColorFormats[i]);
            }

            pipelineStateStream.SampleDesc = DXFormats.DirectX12(desc.AttachmentFormats.SampleCount);
        }

        // RenderState
        {
            ColorAttachmentBlendState[] states =
            [
                desc.RenderState.Blend.ColorAttachment0,
                desc.RenderState.Blend.ColorAttachment1,
                desc.RenderState.Blend.ColorAttachment2,
                desc.RenderState.Blend.ColorAttachment3,
                desc.RenderState.Blend.ColorAttachment4,
                desc.RenderState.Blend.ColorAttachment5,
                desc.RenderState.Blend.ColorAttachment6,
                desc.RenderState.Blend.ColorAttachment7
            ];

            pipelineStateStream.RasterizerState = new()
            {
                FillMode = DXFormats.DirectX12(desc.RenderState.Rasterizer.FillMode),
                CullMode = DXFormats.DirectX12(desc.RenderState.Rasterizer.CullMode),
                FrontCounterClockwise = desc.RenderState.Rasterizer.FrontFace is FrontFace.CounterClockwise,
                DepthBias = desc.RenderState.Rasterizer.DepthBias,
                DepthBiasClamp = desc.RenderState.Rasterizer.DepthBiasClamp,
                SlopeScaledDepthBias = desc.RenderState.Rasterizer.DepthBiasSlopeScale,
                DepthClipEnable = desc.RenderState.Rasterizer.IsDepthClipEnabled,
                MultisampleEnable = desc.AttachmentFormats.SampleCount is not SampleCount.Count1
            };

            pipelineStateStream.DepthStencilState = new()
            {
                DepthEnable = desc.RenderState.DepthStencil.IsDepthEnabled,
                DepthWriteMask = desc.RenderState.DepthStencil.IsDepthWriteEnabled ? DepthWriteMask.All : DepthWriteMask.Zero,
                DepthFunc = DXFormats.DirectX12(desc.RenderState.DepthStencil.DepthCompareOp),
                StencilEnable = desc.RenderState.DepthStencil.IsStencilEnabled,
                StencilReadMask = desc.RenderState.DepthStencil.StencilReadMask,
                StencilWriteMask = desc.RenderState.DepthStencil.StencilWriteMask,
                FrontFace = new()
                {
                    StencilFailOp = DXFormats.DirectX12(desc.RenderState.DepthStencil.FrontFace.FailOp),
                    StencilDepthFailOp = DXFormats.DirectX12(desc.RenderState.DepthStencil.FrontFace.DepthFailOp),
                    StencilPassOp = DXFormats.DirectX12(desc.RenderState.DepthStencil.FrontFace.PassOp),
                    StencilFunc = DXFormats.DirectX12(desc.RenderState.DepthStencil.FrontFace.CompareOp)
                },
                BackFace = new()
                {
                    StencilFailOp = DXFormats.DirectX12(desc.RenderState.DepthStencil.BackFace.FailOp),
                    StencilDepthFailOp = DXFormats.DirectX12(desc.RenderState.DepthStencil.BackFace.DepthFailOp),
                    StencilPassOp = DXFormats.DirectX12(desc.RenderState.DepthStencil.BackFace.PassOp),
                    StencilFunc = DXFormats.DirectX12(desc.RenderState.DepthStencil.BackFace.CompareOp)
                }
            };

            pipelineStateStream.BlendState = new()
            {
                AlphaToCoverageEnable = desc.RenderState.Blend.IsAlphaToCoverageEnabled,
                IndependentBlendEnable = desc.RenderState.Blend.IsIndependentBlendEnabled
            };

            for (int i = 0; i < states.Length; i++)
            {
                ColorAttachmentBlendState blend = states[i];

                pipelineStateStream.BlendState.RenderTarget[i] = new()
                {
                    BlendEnable = blend.IsBlendingEnabled,
                    SrcBlend = DXFormats.DirectX12(blend.SrcRgbFactor),
                    DestBlend = DXFormats.DirectX12(blend.DstRgbFactor),
                    BlendOp = DXFormats.DirectX12(blend.RgbOp),
                    SrcBlendAlpha = DXFormats.DirectX12(blend.SrcAlphaFactor),
                    DestBlendAlpha = DXFormats.DirectX12(blend.DstAlphaFactor),
                    BlendOpAlpha = DXFormats.DirectX12(blend.AlphaOp),
                    LogicOp = LogicOp.Noop,
                    RenderTargetWriteMask = (byte)DXFormats.DirectX12(blend.ColorWrites)
                };
            }
        }

        PipelineStateStreamDesc pipelineStateStreamDesc = new()
        {
            SizeInBytes = (uint)sizeof(PipelineStateStream2),
            PPipelineStateSubobjectStream = &pipelineStateStream
        };

        context.Device.CreatePipelineState(&pipelineStateStreamDesc, SilkMarshal.GuidPtrOf<ID3D12PipelineState>(), (void**)PipelineState.GetAddressOf()).Success();
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
