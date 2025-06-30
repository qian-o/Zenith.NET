namespace Zenith.NET;

public abstract class ResourceFactory(GraphicsContext context)
{
    public GraphicsContext Context { get; } = context;

    internal ResourceValidator Validator { get; } = new(context);

    public SwapChain CreateSwapChain(SwapChainDesc desc)
    {
        if (Context.UseDebugLayer)
        {
            Validator.ValidateSwapChainDesc(desc);
        }

        return CreateSwapChainImpl(desc);
    }

    public Buffer CreateBuffer(BufferDesc desc)
    {
        if (Context.UseDebugLayer)
        {
            Validator.ValidateBufferDesc(desc);
        }

        return CreateBufferImpl(desc);
    }

    public BufferView CreateBufferView(BufferViewDesc desc)
    {
        if (Context.UseDebugLayer)
        {
            Validator.ValidateBufferViewDesc(desc);
        }

        return CreateBufferViewImpl(desc);
    }

    public Texture CreateTexture(TextureDesc desc)
    {
        if (Context.UseDebugLayer)
        {
            Validator.ValidateTextureDesc(desc);
        }

        return CreateTextureImpl(desc);
    }

    public TextureView CreateTextureView(TextureViewDesc desc)
    {
        if (Context.UseDebugLayer)
        {
            Validator.ValidateTextureViewDesc(desc);
        }

        return CreateTextureViewImpl(desc);
    }

    public Sampler CreateSampler(SamplerDesc desc)
    {
        if (Context.UseDebugLayer)
        {
            Validator.ValidateSamplerDesc(desc);
        }

        return CreateSamplerImpl(desc);
    }

    public ResourceLayout CreateResourceLayout(ResourceLayoutDesc desc)
    {
        if (Context.UseDebugLayer)
        {
            Validator.ValidateResourceLayoutDesc(desc);
        }

        return CreateResourceLayoutImpl(desc);
    }

    public ResourceSet CreateResourceSet(ResourceSetDesc desc)
    {
        if (Context.UseDebugLayer)
        {
            Validator.ValidateResourceSetDesc(desc);
        }

        return CreateResourceSetImpl(desc);
    }

    public FrameBuffer CreateFrameBuffer(FrameBufferDesc desc)
    {
        if (Context.UseDebugLayer)
        {
            Validator.ValidateFrameBufferDesc(desc);
        }

        return CreateFrameBufferImpl(desc);
    }

    public Shader CreateShader(ShaderDesc desc)
    {
        if (Context.UseDebugLayer)
        {
            Validator.ValidateShaderDesc(desc);
        }

        return CreateShaderImpl(desc);
    }

    public GraphicsPipeline CreateGraphicsPipeline(GraphicsPipelineDesc desc)
    {
        if (Context.UseDebugLayer)
        {
            Validator.ValidateGraphicsPipelineDesc(desc);
        }

        return CreateGraphicsPipelineImpl(desc);
    }

    public ComputePipeline CreateComputePipeline(ComputePipelineDesc desc)
    {
        if (Context.UseDebugLayer)
        {
            Validator.ValidateComputePipelineDesc(desc);
        }

        return CreateComputePipelineImpl(desc);
    }

    public RayTracingPipeline CreateRayTracingPipeline(RayTracingPipelineDesc desc)
    {
        if (Context.UseDebugLayer)
        {
            Validator.ValidateRayTracingPipelineDesc(desc);
        }

        return CreateRayTracingPipelineImpl(desc);
    }

    protected abstract SwapChain CreateSwapChainImpl(SwapChainDesc desc);

    protected abstract Buffer CreateBufferImpl(BufferDesc desc);

    protected abstract BufferView CreateBufferViewImpl(BufferViewDesc desc);

    protected abstract Texture CreateTextureImpl(TextureDesc desc);

    protected abstract TextureView CreateTextureViewImpl(TextureViewDesc desc);

    protected abstract Sampler CreateSamplerImpl(SamplerDesc desc);

    protected abstract ResourceLayout CreateResourceLayoutImpl(ResourceLayoutDesc desc);

    protected abstract ResourceSet CreateResourceSetImpl(ResourceSetDesc desc);

    protected abstract FrameBuffer CreateFrameBufferImpl(FrameBufferDesc desc);

    protected abstract Shader CreateShaderImpl(ShaderDesc desc);

    protected abstract GraphicsPipeline CreateGraphicsPipelineImpl(GraphicsPipelineDesc desc);

    protected abstract ComputePipeline CreateComputePipelineImpl(ComputePipelineDesc desc);

    protected abstract RayTracingPipeline CreateRayTracingPipelineImpl(RayTracingPipelineDesc desc);
}
