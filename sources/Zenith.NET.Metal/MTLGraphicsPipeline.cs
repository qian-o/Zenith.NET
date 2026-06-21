using Metal.NET;

namespace Zenith.NET.Metal;

internal class MTLGraphicsPipeline : GraphicsPipeline
{
    public MTLDepthStencilState DepthStencilState;

    public MTLRenderPipelineState RenderPipelineState;

    public MTLGraphicsPipeline(MTLGraphicsContext context, GraphicsPipelineDesc desc) : base(context, desc)
    {
        MTL4RenderPipelineDescriptor descriptor = new()
        {
            VertexFunctionDescriptor = desc.VertexShader.Metal().Descriptor,
            FragmentFunctionDescriptor = desc.FragmentShader.Metal().Descriptor,
            InputPrimitiveTopology = MTLFormats.Metal(desc.PrimitiveTopology).TopologyClass
        };

        // InputLayouts
        {
            uint attribute = 0;
            for (int i = 0; i < desc.InputLayouts.Length; i++)
            {
                uint bufferIndex = (uint)(i + 1);

                InputLayout inputLayout = desc.InputLayouts[i];

                descriptor.VertexDescriptor.Layouts[bufferIndex].Stride = inputLayout.StrideInBytes;

                foreach (InputElement element in inputLayout.Elements)
                {
                    descriptor.VertexDescriptor.Attributes[attribute++] = new()
                    {
                        Format = MTLFormats.Metal(element.Format),
                        Offset = element.OffsetInBytes,
                        BufferIndex = bufferIndex
                    };
                }
            }
        }

        // AttachmentFormats and RenderState
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

            descriptor.RasterSampleCount = MTLFormats.Metal(desc.AttachmentFormats.SampleCount);
            descriptor.AlphaToCoverageState = desc.RenderState.Blend.IsAlphaToCoverageEnabled ? MTL4AlphaToCoverageState.Enabled : MTL4AlphaToCoverageState.Disabled;

            for (int i = 0; i < desc.AttachmentFormats.ColorFormats.Length; i++)
            {
                ColorAttachmentBlendState state = desc.RenderState.Blend.IsIndependentBlendEnabled ? states[i] : states[0];

                descriptor.ColorAttachments[(uint)i] = new()
                {
                    PixelFormat = MTLFormats.Metal(desc.AttachmentFormats.ColorFormats[i]).PixelFormat,
                    BlendingState = state.IsBlendingEnabled ? MTL4BlendState.Enabled : MTL4BlendState.Disabled,
                    SourceRGBBlendFactor = MTLFormats.Metal(state.SrcRgbFactor),
                    DestinationRGBBlendFactor = MTLFormats.Metal(state.DstRgbFactor),
                    RgbBlendOperation = MTLFormats.Metal(state.RgbOp),
                    SourceAlphaBlendFactor = MTLFormats.Metal(state.SrcAlphaFactor),
                    DestinationAlphaBlendFactor = MTLFormats.Metal(state.DstAlphaFactor),
                    AlphaBlendOperation = MTLFormats.Metal(state.AlphaOp),
                    WriteMask = MTLFormats.Metal(state.ColorWrites)
                };
            }

            DepthStencilState = context.Device.MakeDepthStencilState(new()
            {
                DepthCompareFunction = desc.RenderState.DepthStencil.IsDepthEnabled ? MTLFormats.Metal(desc.RenderState.DepthStencil.DepthCompareOp) : MTLCompareFunction.Always,
                IsDepthWriteEnabled = desc.RenderState.DepthStencil.IsDepthWriteEnabled,
                FrontFaceStencil = desc.RenderState.DepthStencil.IsStencilEnabled ? new()
                {
                    StencilCompareFunction = MTLFormats.Metal(desc.RenderState.DepthStencil.FrontFace.CompareOp),
                    StencilFailureOperation = MTLFormats.Metal(desc.RenderState.DepthStencil.FrontFace.FailOp),
                    DepthFailureOperation = MTLFormats.Metal(desc.RenderState.DepthStencil.FrontFace.DepthFailOp),
                    DepthStencilPassOperation = MTLFormats.Metal(desc.RenderState.DepthStencil.FrontFace.PassOp),
                    ReadMask = desc.RenderState.DepthStencil.StencilReadMask,
                    WriteMask = desc.RenderState.DepthStencil.StencilWriteMask
                } : MTLStencilDescriptor.Null,
                BackFaceStencil = desc.RenderState.DepthStencil.IsStencilEnabled ? new()
                {
                    StencilCompareFunction = MTLFormats.Metal(desc.RenderState.DepthStencil.BackFace.CompareOp),
                    StencilFailureOperation = MTLFormats.Metal(desc.RenderState.DepthStencil.BackFace.FailOp),
                    DepthFailureOperation = MTLFormats.Metal(desc.RenderState.DepthStencil.BackFace.DepthFailOp),
                    DepthStencilPassOperation = MTLFormats.Metal(desc.RenderState.DepthStencil.BackFace.PassOp),
                    ReadMask = desc.RenderState.DepthStencil.StencilReadMask,
                    WriteMask = desc.RenderState.DepthStencil.StencilWriteMask
                } : MTLStencilDescriptor.Null
            });
        }

        RenderPipelineState = context.Compiler.MakeRenderPipelineState(descriptor, MTL4CompilerTaskOptions.Null, out NSError error);
        error.Success();
    }

    public override nint GetNativeObject(NativeObjectType type)
    {
        return 0;
    }

    protected override void SetResourceName(string name)
    {
    }

    protected override void Destroy()
    {
        RenderPipelineState.Dispose();
        DepthStencilState.Dispose();
    }
}
