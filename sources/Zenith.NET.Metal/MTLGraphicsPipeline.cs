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
            InputPrimitiveTopology = MTLFormats.Metal(desc.PrimitiveTopology)
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
                desc.RenderState.BlendState.ColorAttachment0,
                desc.RenderState.BlendState.ColorAttachment1,
                desc.RenderState.BlendState.ColorAttachment2,
                desc.RenderState.BlendState.ColorAttachment3,
                desc.RenderState.BlendState.ColorAttachment4,
                desc.RenderState.BlendState.ColorAttachment5,
                desc.RenderState.BlendState.ColorAttachment6,
                desc.RenderState.BlendState.ColorAttachment7
            ];

            descriptor.RasterSampleCount = MTLFormats.Metal(desc.AttachmentFormats.SampleCount);
            descriptor.AlphaToCoverageState = desc.RenderState.BlendState.IsAlphaToCoverageEnabled ? MTL4AlphaToCoverageState.Enabled : MTL4AlphaToCoverageState.Disabled;

            for (int i = 0; i < desc.AttachmentFormats.ColorFormats.Length; i++)
            {
                ColorAttachmentBlendState state = desc.RenderState.BlendState.IsIndependentBlendEnabled ? states[i] : states[0];

                descriptor.ColorAttachments[(uint)i] = new()
                {
                    PixelFormat = MTLFormats.Metal(desc.AttachmentFormats.ColorFormats[i]),
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
                DepthCompareFunction = desc.RenderState.DepthStencilState.IsDepthEnabled ? MTLFormats.Metal(desc.RenderState.DepthStencilState.DepthCompareOp) : MTLCompareFunction.Always,
                IsDepthWriteEnabled = desc.RenderState.DepthStencilState.IsDepthWriteEnabled,
                FrontFaceStencil = desc.RenderState.DepthStencilState.IsStencilEnabled ? new()
                {
                    StencilCompareFunction = MTLFormats.Metal(desc.RenderState.DepthStencilState.FrontFace.CompareOp),
                    StencilFailureOperation = MTLFormats.Metal(desc.RenderState.DepthStencilState.FrontFace.FailOp),
                    DepthFailureOperation = MTLFormats.Metal(desc.RenderState.DepthStencilState.FrontFace.DepthFailOp),
                    DepthStencilPassOperation = MTLFormats.Metal(desc.RenderState.DepthStencilState.FrontFace.PassOp),
                    ReadMask = desc.RenderState.DepthStencilState.StencilReadMask,
                    WriteMask = desc.RenderState.DepthStencilState.StencilWriteMask
                } : MTLStencilDescriptor.Null,
                BackFaceStencil = desc.RenderState.DepthStencilState.IsStencilEnabled ? new()
                {
                    StencilCompareFunction = MTLFormats.Metal(desc.RenderState.DepthStencilState.BackFace.CompareOp),
                    StencilFailureOperation = MTLFormats.Metal(desc.RenderState.DepthStencilState.BackFace.FailOp),
                    DepthFailureOperation = MTLFormats.Metal(desc.RenderState.DepthStencilState.BackFace.DepthFailOp),
                    DepthStencilPassOperation = MTLFormats.Metal(desc.RenderState.DepthStencilState.BackFace.PassOp),
                    ReadMask = desc.RenderState.DepthStencilState.StencilReadMask,
                    WriteMask = desc.RenderState.DepthStencilState.StencilWriteMask
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
