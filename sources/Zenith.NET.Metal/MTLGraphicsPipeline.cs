using Metal.NET;

namespace Zenith.NET.Metal;

internal class MTLGraphicsPipeline : GraphicsPipeline
{
    public MTLDepthStencilState DepthStencilState;

    public MTLRenderPipelineState RenderPipelineState;

    public MTLGraphicsPipeline(MTLGraphicsContext context, GraphicsPipelineDesc desc) : base(context, desc)
    {
        VertexBufferStartIndex = desc.ResourceLayout is not null ? desc.ResourceLayout.Metal().BufferCount : 0;

        MTLRenderPipelineDescriptor descriptor = new()
        {
            VertexFunction = desc.Vertex.Metal().Function,
            FragmentFunction = desc.Pixel.Metal().Function
        };

        // RenderStates - Output
        {
            DepthStencilState = context.Device.NewDepthStencilState(new()
            {
                DepthWriteEnabled = desc.RenderStates.DepthStencilState.DepthWriteEnable,
                DepthCompareFunction = desc.RenderStates.DepthStencilState.DepthEnable ? MTLFormats.Metal(desc.RenderStates.DepthStencilState.DepthFunc) : MTLCompareFunction.Always,
                FrontFaceStencil = desc.RenderStates.DepthStencilState.StencilEnable ? new()
                {
                    StencilFailureOperation = MTLFormats.Metal(desc.RenderStates.DepthStencilState.FrontFace.StencilFailOp),
                    DepthFailureOperation = MTLFormats.Metal(desc.RenderStates.DepthStencilState.FrontFace.StencilDepthFailOp),
                    DepthStencilPassOperation = MTLFormats.Metal(desc.RenderStates.DepthStencilState.FrontFace.StencilPassOp),
                    StencilCompareFunction = MTLFormats.Metal(desc.RenderStates.DepthStencilState.FrontFace.StencilFunc),
                    ReadMask = desc.RenderStates.DepthStencilState.StencilReadMask,
                    WriteMask = desc.RenderStates.DepthStencilState.StencilWriteMask
                } : MTLStencilDescriptor.Null,
                BackFaceStencil = desc.RenderStates.DepthStencilState.StencilEnable ? new()
                {
                    StencilFailureOperation = MTLFormats.Metal(desc.RenderStates.DepthStencilState.BackFace.StencilFailOp),
                    DepthFailureOperation = MTLFormats.Metal(desc.RenderStates.DepthStencilState.BackFace.StencilDepthFailOp),
                    DepthStencilPassOperation = MTLFormats.Metal(desc.RenderStates.DepthStencilState.BackFace.StencilPassOp),
                    StencilCompareFunction = MTLFormats.Metal(desc.RenderStates.DepthStencilState.BackFace.StencilFunc),
                    ReadMask = desc.RenderStates.DepthStencilState.StencilReadMask,
                    WriteMask = desc.RenderStates.DepthStencilState.StencilWriteMask
                } : MTLStencilDescriptor.Null
            });

            descriptor.AlphaToCoverageEnabled = desc.RenderStates.BlendState.AlphaToCoverageEnable;

            BlendStateRenderTarget[] blendStateRenderTargets =
            [
                desc.RenderStates.BlendState.RenderTarget0,
                desc.RenderStates.BlendState.RenderTarget1,
                desc.RenderStates.BlendState.RenderTarget2,
                desc.RenderStates.BlendState.RenderTarget3,
                desc.RenderStates.BlendState.RenderTarget4,
                desc.RenderStates.BlendState.RenderTarget5,
                desc.RenderStates.BlendState.RenderTarget6,
                desc.RenderStates.BlendState.RenderTarget7
            ];

            for (int i = 0; i < blendStateRenderTargets.Length; i++)
            {
                BlendStateRenderTarget target = desc.RenderStates.BlendState.IndependentBlendEnable ? blendStateRenderTargets[i] : blendStateRenderTargets[0];

                descriptor.ColorAttachments[(uint)i] = new()
                {
                    PixelFormat = i < desc.Output.ColorAttachments.Length ? MTLFormats.Metal(desc.Output.ColorAttachments[i]) : MTLPixelFormat.Invalid,
                    BlendingEnabled = target.BlendEnable,
                    SourceRGBBlendFactor = MTLFormats.Metal(target.SrcBlend),
                    DestinationRGBBlendFactor = MTLFormats.Metal(target.DestBlend),
                    RgbBlendOperation = MTLFormats.Metal(target.BlendOp),
                    SourceAlphaBlendFactor = MTLFormats.Metal(target.SrcBlendAlpha),
                    DestinationAlphaBlendFactor = MTLFormats.Metal(target.DestBlendAlpha),
                    AlphaBlendOperation = MTLFormats.Metal(target.BlendOpAlpha),
                    WriteMask = MTLFormats.Metal(target.Flags)
                };
            }

            if (desc.Output.DepthStencilAttachment.HasValue)
            {
                if (ZenithHelper.HasDepth(desc.Output.DepthStencilAttachment.Value))
                {
                    descriptor.DepthAttachmentPixelFormat = MTLFormats.Metal(desc.Output.DepthStencilAttachment.Value);
                }

                if (ZenithHelper.HasStencil(desc.Output.DepthStencilAttachment.Value))
                {
                    descriptor.StencilAttachmentPixelFormat = MTLFormats.Metal(desc.Output.DepthStencilAttachment.Value);
                }
            }

            descriptor.RasterSampleCount = MTLFormats.Metal(desc.Output.SampleCount);
        }

        // InputLayouts
        {
            uint attribute = 0;
            for (int i = 0; i < desc.InputLayouts.Length; i++)
            {
                InputLayout inputLayout = desc.InputLayouts[i];

                descriptor.VertexDescriptor.Layouts[(uint)i].Stride = inputLayout.StrideInBytes;

                foreach (InputElement element in inputLayout.Elements)
                {
                    descriptor.VertexDescriptor.Attributes[attribute++] = new()
                    {
                        Format = MTLFormats.Metal(element.Format),
                        Offset = element.OffsetInBytes,
                        BufferIndex = VertexBufferStartIndex + (uint)i
                    };
                }
            }
        }

        // PrimitiveTopology
        {
            descriptor.InputPrimitiveTopology = MTLFormats.Metal(desc.PrimitiveTopology);
        }

        RenderPipelineState = context.Device.NewRenderPipelineState(descriptor, out NSError error);
        error.Success();
    }

    public uint VertexBufferStartIndex { get; }

    protected override void SetResourceName(string name)
    {
    }

    protected override void Destroy()
    {
        RenderPipelineState.Dispose();
        DepthStencilState.Dispose();
    }
}
