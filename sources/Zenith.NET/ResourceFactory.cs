namespace Zenith.NET;

public abstract class ResourceFactory(GraphicsContext context)
{
    public GraphicsContext Context { get; } = context;

    public SwapChain CreateSwapChain(SwapChainDesc desc)
    {
        if (!desc.Validate())
        {
            throw new ArgumentException("Invalid swap chain description.", nameof(desc));
        }

        return CreateSwapChainImpl(desc);
    }

    public FrameBuffer CreateFrameBuffer(FrameBufferDesc desc)
    {
        if (!desc.Validate())
        {
            throw new ArgumentException("Invalid frame buffer description.", nameof(desc));
        }

        return CreateFrameBufferImpl(desc);
    }

    public Buffer CreateBuffer(BufferDesc desc)
    {
        if (!desc.Validate())
        {
            throw new ArgumentException("Invalid buffer description.", nameof(desc));
        }

        return CreateBufferImpl(desc);
    }

    public Texture CreateTexture(TextureDesc desc)
    {
        if (!desc.Validate())
        {
            throw new ArgumentException("Invalid texture description.", nameof(desc));
        }

        return CreateTextureImpl(desc);
    }

    public Sampler CreateSampler(SamplerDesc desc)
    {
        if (!desc.Validate())
        {
            throw new ArgumentException("Invalid sampler description.", nameof(desc));
        }

        return CreateSamplerImpl(desc);
    }

    public Shader CreateShader(ShaderDesc desc)
    {
        if (!desc.Validate())
        {
            throw new ArgumentException("Invalid shader description.", nameof(desc));
        }

        return CreateShaderImpl(desc);
    }

    public ResourceLayout CreateResourceLayout(ResourceLayoutDesc desc)
    {
        if (!desc.Validate())
        {
            throw new ArgumentException("Invalid resource layout description.", nameof(desc));
        }

        return CreateResourceLayoutImpl(desc);
    }

    public ResourceSet CreateResourceSet(ResourceSetDesc desc)
    {
        if (!desc.Validate())
        {
            throw new ArgumentException("Invalid resource set description.", nameof(desc));
        }

        return CreateResourceSetImpl(desc);
    }

    public GraphicsPipeline CreateGraphicsPipeline(GraphicsPipelineDesc desc)
    {
        if (!desc.Validate())
        {
            throw new ArgumentException("Invalid graphics pipeline description.", nameof(desc));
        }

        return CreateGraphicsPipelineImpl(desc);
    }

    public ComputePipeline CreateComputePipeline(ComputePipelineDesc desc)
    {
        if (!desc.Validate())
        {
            throw new ArgumentException("Invalid compute pipeline description.", nameof(desc));
        }

        return CreateComputePipelineImpl(desc);
    }

    public RayTracingPipeline CreateRayTracingPipeline(RayTracingPipelineDesc desc)
    {
        if (!desc.Validate())
        {
            throw new ArgumentException("Invalid ray tracing pipeline description.", nameof(desc));
        }

        return CreateRayTracingPipelineImpl(desc);
    }

    public CommandQueue CreateCommandQueue(CommandQueueType type)
    {
        if (!Enum.IsDefined(type))
        {
            throw new ArgumentException("Invalid command queue type.", nameof(type));
        }

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
