namespace Zenith.NET;

public abstract class ResourceFactory(GraphicsContext context)
{
    public GraphicsContext Context { get; } = context;

    public SwapChain CreateSwapChain(SwapChainDesc desc)
    {
        if (Context.UseDebugLayer)
        {
            throw new NotImplementedException("SwapChain validation is not implemented yet.");
        }

        return CreateSwapChainImpl(desc);
    }

    public FrameBuffer CreateFrameBuffer(FrameBufferDesc desc)
    {
        if (Context.UseDebugLayer)
        {
            throw new NotImplementedException("FrameBuffer validation is not implemented yet.");
        }

        return CreateFrameBufferImpl(desc);
    }

    public Buffer CreateBuffer(BufferDesc desc)
    {
        if (Context.UseDebugLayer)
        {
            throw new NotImplementedException("Buffer validation is not implemented yet.");
        }

        return CreateBufferImpl(desc);
    }

    public Texture CreateTexture(TextureDesc desc)
    {
        if (Context.UseDebugLayer)
        {
            throw new NotImplementedException("Texture validation is not implemented yet.");
        }

        return CreateTextureImpl(desc);
    }

    public Sampler CreateSampler(SamplerDesc desc)
    {
        if (Context.UseDebugLayer)
        {
            throw new NotImplementedException("Sampler validation is not implemented yet.");
        }

        return CreateSamplerImpl(desc);
    }

    public Shader CreateShader(ShaderDesc desc)
    {
        if (Context.UseDebugLayer)
        {
            throw new NotImplementedException("Shader validation is not implemented yet.");
        }

        return CreateShaderImpl(desc);
    }

    public ResourceLayout CreateResourceLayout(ResourceLayoutDesc desc)
    {
        if (Context.UseDebugLayer)
        {
            throw new NotImplementedException("ResourceLayout validation is not implemented yet.");
        }

        return CreateResourceLayoutImpl(desc);
    }

    public ResourceSet CreateResourceSet(ResourceSetDesc desc)
    {
        if (Context.UseDebugLayer)
        {
            throw new NotImplementedException("ResourceSet validation is not implemented yet.");
        }

        return CreateResourceSetImpl(desc);
    }

    public GraphicsPipeline CreateGraphicsPipeline(GraphicsPipelineDesc desc)
    {
        if (Context.UseDebugLayer)
        {
            throw new NotImplementedException("GraphicsPipeline validation is not implemented yet.");
        }

        return CreateGraphicsPipelineImpl(desc);
    }

    public ComputePipeline CreateComputePipeline(ComputePipelineDesc desc)
    {
        if (Context.UseDebugLayer)
        {
            throw new NotImplementedException("ComputePipeline validation is not implemented yet.");
        }

        return CreateComputePipelineImpl(desc);
    }

    public RayTracingPipeline CreateRayTracingPipeline(RayTracingPipelineDesc desc)
    {
        if (Context.UseDebugLayer)
        {
            throw new NotImplementedException("RayTracingPipeline validation is not implemented yet.");
        }

        return CreateRayTracingPipelineImpl(desc);
    }

    protected abstract SwapChain CreateSwapChainImpl(SwapChainDesc desc);

    protected abstract FrameBuffer CreateFrameBufferImpl(FrameBufferDesc desc);

    protected abstract Buffer CreateBufferImpl(BufferDesc desc);

    protected abstract Texture CreateTextureImpl(TextureDesc desc);

    protected abstract Sampler CreateSamplerImpl(SamplerDesc desc);

    protected abstract Shader CreateShaderImpl(ShaderDesc desc);

    protected abstract ResourceLayout CreateResourceLayoutImpl(ResourceLayoutDesc desc);

    protected abstract ResourceSet CreateResourceSetImpl(ResourceSetDesc desc);

    protected abstract GraphicsPipeline CreateGraphicsPipelineImpl(GraphicsPipelineDesc desc);

    protected abstract ComputePipeline CreateComputePipelineImpl(ComputePipelineDesc desc);

    protected abstract RayTracingPipeline CreateRayTracingPipelineImpl(RayTracingPipelineDesc desc);
}
