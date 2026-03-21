using Metal.NET;

namespace Zenith.NET.Metal;

internal class MTLGraphicsPipeline : GraphicsPipeline
{
    public MTLRenderPipelineState RenderPipelineState;

    public MTLDepthStencilState DepthStencilState;

    public MTLGraphicsPipeline(MTLGraphicsContext context, GraphicsPipelineDesc desc) : base(context, desc)
    {
        VertexBufferStartIndex = desc.ResourceLayout is not null ? desc.ResourceLayout.Metal().BufferCount : 0;

        MTL4RenderPipelineDescriptor descriptor = new()
        {
            VertexFunctionDescriptor = new MTL4LibraryFunctionDescriptor()
            {
                Name = desc.Vertex.Desc.EntryPoint,
                Library = desc.Vertex.Metal().Library
            },
            FragmentFunctionDescriptor = new MTL4LibraryFunctionDescriptor()
            {
                Name = desc.Pixel.Desc.EntryPoint,
                Library = desc.Pixel.Metal().Library
            },
            InputPrimitiveTopology = MTLFormats.Metal(desc.PrimitiveTopology)
        };

        // RenderStates - Output
        {
            descriptor.AlphaToCoverageState = desc.RenderStates.BlendState.AlphaToCoverageEnable ? MTL4AlphaToCoverageState.Enabled : MTL4AlphaToCoverageState.Disabled;

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
                    BlendingState = target.BlendEnable ? MTL4BlendState.Enabled : MTL4BlendState.Disabled,
                    SourceRGBBlendFactor = MTLFormats.Metal(target.SrcBlend),
                    DestinationRGBBlendFactor = MTLFormats.Metal(target.DestBlend),
                    RgbBlendOperation = MTLFormats.Metal(target.BlendOp),
                    SourceAlphaBlendFactor = MTLFormats.Metal(target.SrcBlendAlpha),
                    DestinationAlphaBlendFactor = MTLFormats.Metal(target.DestBlendAlpha),
                    AlphaBlendOperation = MTLFormats.Metal(target.BlendOpAlpha),
                    WriteMask = MTLFormats.Metal(target.Flags)
                };
            }

            descriptor.RasterSampleCount = MTLFormats.Metal(desc.Output.SampleCount);

            DepthStencilState = context.Device.MakeDepthStencilState(new()
            {
                DepthCompareFunction = desc.RenderStates.DepthStencilState.DepthEnable ? MTLFormats.Metal(desc.RenderStates.DepthStencilState.DepthFunc) : MTLCompareFunction.Always,
                IsDepthWriteEnabled = desc.RenderStates.DepthStencilState.DepthWriteEnable,
                FrontFaceStencil = desc.RenderStates.DepthStencilState.StencilEnable ? new()
                {
                    StencilCompareFunction = MTLFormats.Metal(desc.RenderStates.DepthStencilState.FrontFace.StencilFunc),
                    StencilFailureOperation = MTLFormats.Metal(desc.RenderStates.DepthStencilState.FrontFace.StencilFailOp),
                    DepthFailureOperation = MTLFormats.Metal(desc.RenderStates.DepthStencilState.FrontFace.StencilDepthFailOp),
                    DepthStencilPassOperation = MTLFormats.Metal(desc.RenderStates.DepthStencilState.FrontFace.StencilPassOp),
                    ReadMask = desc.RenderStates.DepthStencilState.StencilReadMask,
                    WriteMask = desc.RenderStates.DepthStencilState.StencilWriteMask
                } : MTLStencilDescriptor.Null,
                BackFaceStencil = desc.RenderStates.DepthStencilState.StencilEnable ? new()
                {
                    StencilCompareFunction = MTLFormats.Metal(desc.RenderStates.DepthStencilState.BackFace.StencilFunc),
                    StencilFailureOperation = MTLFormats.Metal(desc.RenderStates.DepthStencilState.BackFace.StencilFailOp),
                    DepthFailureOperation = MTLFormats.Metal(desc.RenderStates.DepthStencilState.BackFace.StencilDepthFailOp),
                    DepthStencilPassOperation = MTLFormats.Metal(desc.RenderStates.DepthStencilState.BackFace.StencilPassOp),
                    ReadMask = desc.RenderStates.DepthStencilState.StencilReadMask,
                    WriteMask = desc.RenderStates.DepthStencilState.StencilWriteMask
                } : MTLStencilDescriptor.Null
            });
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

        RenderPipelineState = context.Compiler.MakeRenderPipelineState(descriptor, MTLRenderPipelineState.Null, out NSError error);
        error.Success();
    }

    public uint VertexBufferStartIndex { get; }

    public void Bind(MTL4RenderCommandEncoder encoder)
    {
        encoder.SetRenderPipelineState(RenderPipelineState);

        encoder.SetCullMode(MTLFormats.Metal(Desc.RenderStates.RasterizerState.CullMode));

        encoder.SetDepthClipMode(Desc.RenderStates.RasterizerState.DepthClipEnable ? MTLDepthClipMode.Clip : MTLDepthClipMode.Clamp);
        encoder.SetDepthBias(Desc.RenderStates.RasterizerState.DepthBias, Desc.RenderStates.RasterizerState.SlopeScaledDepthBias, Desc.RenderStates.RasterizerState.DepthBiasClamp);

        encoder.SetTriangleFillMode(MTLFormats.Metal(Desc.RenderStates.RasterizerState.FillMode));

        if (Desc.RenderStates.BlendFactor.HasValue)
        {
            encoder.SetBlendColor(Desc.RenderStates.BlendFactor.Value.X, Desc.RenderStates.BlendFactor.Value.Y, Desc.RenderStates.BlendFactor.Value.Z, Desc.RenderStates.BlendFactor.Value.W);
        }

        encoder.SetDepthStencilState(DepthStencilState);
        encoder.SetStencilReferenceValue(Desc.RenderStates.StencilReference);

        encoder.SetFrontFacing(MTLFormats.Metal(Desc.RenderStates.RasterizerState.FrontFace));
    }

    protected override void SetResourceName(string name)
    {
    }

    protected override void Destroy()
    {
        DepthStencilState.Dispose();
        RenderPipelineState.Dispose();
    }
}
