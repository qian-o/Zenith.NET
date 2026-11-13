using Metal.NET;

namespace Zenith.NET.Metal;

internal class MTLMeshShadingPipeline : MeshShadingPipeline
{
    public MTLDepthStencilState DepthStencilState;

    public MTLRenderPipelineState RenderPipelineState;

    public MTLMeshShadingPipeline(MTLGraphicsContext context, MeshShadingPipelineDesc desc) : base(context, desc)
    {
        using MTL4MeshRenderPipelineDescriptor descriptor = new()
        {
            MeshFunctionDescriptor = desc.MeshShader.Metal().Descriptor,
            FragmentFunctionDescriptor = desc.FragmentShader.Metal().Descriptor,
            RequiredThreadsPerMeshThreadgroup = new(desc.MeshShader.Desc.ThreadGroupSize.X, desc.MeshShader.Desc.ThreadGroupSize.Y, desc.MeshShader.Desc.ThreadGroupSize.Z)
        };

        if (desc.TaskShader is not null)
        {
            descriptor.ObjectFunctionDescriptor = desc.TaskShader.Metal().Descriptor;
            descriptor.RequiredThreadsPerObjectThreadgroup = new(desc.TaskShader.Desc.ThreadGroupSize.X, desc.TaskShader.Desc.ThreadGroupSize.Y, desc.TaskShader.Desc.ThreadGroupSize.Z);
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

                MTL4RenderPipelineColorAttachmentDescriptor colorAttachment = descriptor.ColorAttachments[(uint)i];
                colorAttachment.PixelFormat = MTLFormats.Metal(desc.AttachmentFormats.ColorFormats[i]);
                colorAttachment.BlendingState = state.IsBlendingEnabled ? MTL4BlendState.Enabled : MTL4BlendState.Disabled;
                colorAttachment.SourceRGBBlendFactor = MTLFormats.Metal(state.SrcRgbFactor);
                colorAttachment.DestinationRGBBlendFactor = MTLFormats.Metal(state.DstRgbFactor);
                colorAttachment.RgbBlendOperation = MTLFormats.Metal(state.RgbOp);
                colorAttachment.SourceAlphaBlendFactor = MTLFormats.Metal(state.SrcAlphaFactor);
                colorAttachment.DestinationAlphaBlendFactor = MTLFormats.Metal(state.DstAlphaFactor);
                colorAttachment.AlphaBlendOperation = MTLFormats.Metal(state.AlphaOp);
                colorAttachment.WriteMask = MTLFormats.Metal(state.ColorWrites);
            }

            using MTLDepthStencilDescriptor depthStencilDescriptor = new()
            {
                DepthCompareFunction = desc.RenderState.DepthStencil.IsDepthEnabled ? MTLFormats.Metal(desc.RenderState.DepthStencil.DepthCompareOp) : MTLCompareFunction.Always,
                IsDepthWriteEnabled = desc.RenderState.DepthStencil.IsDepthWriteEnabled
            };

            if (desc.RenderState.DepthStencil.IsStencilEnabled)
            {
                MTLStencilDescriptor frontFaceStencil = depthStencilDescriptor.FrontFaceStencil;
                frontFaceStencil.StencilCompareFunction = MTLFormats.Metal(desc.RenderState.DepthStencil.FrontFace.CompareOp);
                frontFaceStencil.StencilFailureOperation = MTLFormats.Metal(desc.RenderState.DepthStencil.FrontFace.FailOp);
                frontFaceStencil.DepthFailureOperation = MTLFormats.Metal(desc.RenderState.DepthStencil.FrontFace.DepthFailOp);
                frontFaceStencil.DepthStencilPassOperation = MTLFormats.Metal(desc.RenderState.DepthStencil.FrontFace.PassOp);
                frontFaceStencil.ReadMask = desc.RenderState.DepthStencil.StencilReadMask;
                frontFaceStencil.WriteMask = desc.RenderState.DepthStencil.StencilWriteMask;

                MTLStencilDescriptor backFaceStencil = depthStencilDescriptor.BackFaceStencil;
                backFaceStencil.StencilCompareFunction = MTLFormats.Metal(desc.RenderState.DepthStencil.BackFace.CompareOp);
                backFaceStencil.StencilFailureOperation = MTLFormats.Metal(desc.RenderState.DepthStencil.BackFace.FailOp);
                backFaceStencil.DepthFailureOperation = MTLFormats.Metal(desc.RenderState.DepthStencil.BackFace.DepthFailOp);
                backFaceStencil.DepthStencilPassOperation = MTLFormats.Metal(desc.RenderState.DepthStencil.BackFace.PassOp);
                backFaceStencil.ReadMask = desc.RenderState.DepthStencil.StencilReadMask;
                backFaceStencil.WriteMask = desc.RenderState.DepthStencil.StencilWriteMask;
            }

            DepthStencilState = context.Device.MakeDepthStencilState(depthStencilDescriptor);
        }

        RenderPipelineState = context.Compiler.MakeRenderPipelineState(descriptor, default(MTL4CompilerTaskOptions), out NSError error);
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