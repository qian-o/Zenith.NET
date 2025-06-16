namespace Zenith.NET;

public abstract class ResourceFactory(GraphicsContext context)
{
    public GraphicsContext Context { get; } = context;

    public SwapChain CreateSwapChain(SwapChainDesc desc)
    {
        return CreateSwapChainImpl(desc);
    }

    public FrameBuffer CreateFrameBuffer(FrameBufferDesc desc)
    {
        return CreateFrameBufferImpl(desc);
    }

    public Buffer CreateBuffer(BufferDesc desc)
    {
        return CreateBufferImpl(desc);
    }

    public Texture CreateTexture(TextureDesc desc)
    {
        return CreateTextureImpl(desc);
    }

    public Sampler CreateSampler(SamplerDesc desc)
    {
        return CreateSamplerImpl(desc);
    }

    public Shader CreateShader(ShaderDesc desc)
    {
        return CreateShaderImpl(desc);
    }

    public ResourceLayout CreateResourceLayout(ResourceLayoutDesc desc)
    {
        return CreateResourceLayoutImpl(desc);
    }

    public ResourceSet CreateResourceSet(ResourceSetDesc desc)
    {
        return CreateResourceSetImpl(desc);
    }

    public GraphicsPipeline CreateGraphicsPipeline(GraphicsPipelineDesc desc)
    {
        return CreateGraphicsPipelineImpl(desc);
    }

    public ComputePipeline CreateComputePipeline(ComputePipelineDesc desc)
    {
        return CreateComputePipelineImpl(desc);
    }

    public RayTracingPipeline CreateRayTracingPipeline(RayTracingPipelineDesc desc)
    {
        return CreateRayTracingPipelineImpl(desc);
    }

    public CommandQueue CreateCommandQueue(CommandQueueType type)
    {
        return CreateCommandQueueImpl(type);
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

    protected abstract CommandQueue CreateCommandQueueImpl(CommandQueueType type);
}
