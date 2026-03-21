using Metal.NET;

namespace Zenith.NET.Metal;

internal class MTLMeshShadingPipeline : MeshShadingPipeline
{
    public MTLRenderPipelineState RenderPipelineState;

    public MTLDepthStencilState DepthStencilState;

    public MTLMeshShadingPipeline(MTLGraphicsContext context, MeshShadingPipelineDesc desc) : base(context, desc)
    {
        MTL4MeshRenderPipelineDescriptor descriptor = new()
        {
            MeshFunctionDescriptor = desc.Mesh.Metal().Descriptor,
            FragmentFunctionDescriptor = desc.Pixel.Metal().Descriptor,
            RequiredThreadsPerMeshThreadgroup = new(desc.MeshThreadGroupSizeX, desc.MeshThreadGroupSizeY, desc.MeshThreadGroupSizeZ)
        };

        if (desc.Amplification is not null)
        {
            descriptor.ObjectFunctionDescriptor = desc.Amplification.Metal().Descriptor;
            descriptor.RequiredThreadsPerObjectThreadgroup = new(desc.ObjectThreadGroupSizeX, desc.ObjectThreadGroupSizeY, desc.ObjectThreadGroupSizeZ);
        }

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

        RenderPipelineState = context.Compiler.MakeRenderPipelineState(descriptor, MTL4CompilerTaskOptions.Null, out NSError error);
        error.Success();
    }

    public void Bind(MTL4RenderCommandEncoder commandEncoder)
    {
        commandEncoder.SetRenderPipelineState(RenderPipelineState);

        commandEncoder.SetCullMode(MTLFormats.Metal(Desc.RenderStates.RasterizerState.CullMode));

        commandEncoder.SetDepthClipMode(Desc.RenderStates.RasterizerState.DepthClipEnable ? MTLDepthClipMode.Clip : MTLDepthClipMode.Clamp);
        commandEncoder.SetDepthBias(Desc.RenderStates.RasterizerState.DepthBias, Desc.RenderStates.RasterizerState.SlopeScaledDepthBias, Desc.RenderStates.RasterizerState.DepthBiasClamp);

        commandEncoder.SetTriangleFillMode(MTLFormats.Metal(Desc.RenderStates.RasterizerState.FillMode));

        if (Desc.RenderStates.BlendFactor.HasValue)
        {
            commandEncoder.SetBlendColor(Desc.RenderStates.BlendFactor.Value.X, Desc.RenderStates.BlendFactor.Value.Y, Desc.RenderStates.BlendFactor.Value.Z, Desc.RenderStates.BlendFactor.Value.W);
        }

        commandEncoder.SetDepthStencilState(DepthStencilState);
        commandEncoder.SetStencilReferenceValue(Desc.RenderStates.StencilReference);

        commandEncoder.SetFrontFacing(MTLFormats.Metal(Desc.RenderStates.RasterizerState.FrontFace));
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
