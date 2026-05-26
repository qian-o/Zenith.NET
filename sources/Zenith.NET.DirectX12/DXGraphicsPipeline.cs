using Silk.NET.Core.Native;
using Silk.NET.Direct3D12;
using Silk.NET.DXGI;

namespace Zenith.NET.DirectX12;

internal unsafe class DXGraphicsPipeline : GraphicsPipeline
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

        // InputLayouts
        {
            List<InputElementDesc> inputElementDescs = [];
            for (int i = 0; i < desc.InputLayouts.Length; i++)
            {
                InputLayout inputLayout = desc.InputLayouts[i];

                foreach (InputElement element in inputLayout.InputElements)
                {
                    inputElementDescs.Add(new()
                    {
                        SemanticName = (byte*)ZenithMarshal.StringToPointer(scope, element.Semantic.ToString().ToUpper(), StringEncoding.UTF8),
                        SemanticIndex = element.SemanticIndex,
                        Format = DXFormats.DirectX12(element.Format),
                        InputSlot = (uint)i,
                        AlignedByteOffset = element.OffsetInBytes
                    });
                }
            }

            graphicsPipelineStateDesc.InputLayout = new()
            {
                PInputElementDescs = (InputElementDesc*)ZenithMarshal.AllocateAndFill(scope, [.. inputElementDescs]),
                NumElements = (uint)inputElementDescs.Count
            };
        }

        // AttachmentFormats
        {
            graphicsPipelineStateDesc.NumRenderTargets = (uint)desc.AttachmentFormats.ColorFormats.Length;

            for (int i = 0; i < desc.AttachmentFormats.ColorFormats.Length; i++)
            {
                graphicsPipelineStateDesc.RTVFormats[i] = DXFormats.DirectX12(desc.AttachmentFormats.ColorFormats[i]);
            }

            graphicsPipelineStateDesc.DSVFormat = desc.AttachmentFormats.DepthStencilFormat.HasValue ? DXFormats.DirectX12(desc.AttachmentFormats.DepthStencilFormat.Value) : Format.FormatUnknown;

            graphicsPipelineStateDesc.SampleDesc = DXFormats.DirectX12(desc.AttachmentFormats.SampleCount);
        }

        // RenderState
        {
            ColorAttachmentBlendState[] colorAttachmentBlendStates =
            [
                desc.RenderState.BlendState.ColorAttachment0,
                desc.RenderState.BlendState.ColorAttachment1,
                desc.RenderState.BlendState.ColorAttachment2,
                desc.RenderState.BlendState.ColorAttachment3,
                desc.RenderState.BlendState.ColorAttachment4,
                desc.RenderState.BlendState.ColorAttachment5,
                desc.RenderState.BlendState.ColorAttachment6,
                desc.RenderState.BlendState.ColorAttachment7
            ];

            graphicsPipelineStateDesc.RasterizerState = new()
            {
                FillMode = DXFormats.DirectX12(desc.RenderState.RasterizerState.FillMode),
                CullMode = DXFormats.DirectX12(desc.RenderState.RasterizerState.CullMode),
                FrontCounterClockwise = desc.RenderState.RasterizerState.FrontFace is FrontFace.CounterClockwise,
                DepthBias = desc.RenderState.RasterizerState.DepthBias,
                DepthBiasClamp = desc.RenderState.RasterizerState.DepthBiasClamp,
                SlopeScaledDepthBias = desc.RenderState.RasterizerState.DepthBiasSlopeScale,
                DepthClipEnable = desc.RenderState.RasterizerState.IsDepthClipEnabled,
                MultisampleEnable = desc.AttachmentFormats.SampleCount is not SampleCount.Count1
            };

            graphicsPipelineStateDesc.DepthStencilState = new()
            {
                DepthEnable = desc.RenderState.DepthStencilState.IsDepthEnabled,
                DepthWriteMask = desc.RenderState.DepthStencilState.IsDepthWriteEnabled ? DepthWriteMask.All : DepthWriteMask.Zero,
                DepthFunc = DXFormats.DirectX12(desc.RenderState.DepthStencilState.DepthCompareOp),
                StencilEnable = desc.RenderState.DepthStencilState.IsStencilEnabled,
                StencilReadMask = desc.RenderState.DepthStencilState.StencilReadMask,
                StencilWriteMask = desc.RenderState.DepthStencilState.StencilWriteMask,
                FrontFace = new()
                {
                    StencilFailOp = DXFormats.DirectX12(desc.RenderState.DepthStencilState.FrontFace.FailOp),
                    StencilDepthFailOp = DXFormats.DirectX12(desc.RenderState.DepthStencilState.FrontFace.DepthFailOp),
                    StencilPassOp = DXFormats.DirectX12(desc.RenderState.DepthStencilState.FrontFace.PassOp),
                    StencilFunc = DXFormats.DirectX12(desc.RenderState.DepthStencilState.FrontFace.CompareOp)
                },
                BackFace = new()
                {
                    StencilFailOp = DXFormats.DirectX12(desc.RenderState.DepthStencilState.BackFace.FailOp),
                    StencilDepthFailOp = DXFormats.DirectX12(desc.RenderState.DepthStencilState.BackFace.DepthFailOp),
                    StencilPassOp = DXFormats.DirectX12(desc.RenderState.DepthStencilState.BackFace.PassOp),
                    StencilFunc = DXFormats.DirectX12(desc.RenderState.DepthStencilState.BackFace.CompareOp)
                }
            };

            graphicsPipelineStateDesc.BlendState = new()
            {
                AlphaToCoverageEnable = desc.RenderState.BlendState.IsAlphaToCoverageEnabled,
                IndependentBlendEnable = desc.RenderState.BlendState.IsIndependentBlendEnabled
            };

            for (int i = 0; i < colorAttachmentBlendStates.Length; i++)
            {
                ColorAttachmentBlendState blend = colorAttachmentBlendStates[i];

                graphicsPipelineStateDesc.BlendState.RenderTarget[i] = new()
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

        context.Device10.CreateGraphicsPipelineState(&graphicsPipelineStateDesc, SilkMarshal.GuidPtrOf<ID3D12PipelineState>(), (void**)PipelineState.GetAddressOf()).Success();
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
