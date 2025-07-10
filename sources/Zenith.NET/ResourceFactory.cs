namespace Zenith.NET;

public abstract class ResourceFactory(GraphicsContext context)
{
    public GraphicsContext Context { get; } = context;

    public Buffer CreateBuffer(BufferDesc desc)
    {
        Context.Validator?.BufferDesc(desc);

        return CreateBufferImpl(desc);
    }

    public BufferView CreateBufferView(BufferViewDesc desc)
    {
        Context.Validator?.BufferViewDesc(desc);

        return CreateBufferViewImpl(desc);
    }

    public Texture CreateTexture(TextureDesc desc)
    {
        Context.Validator?.TextureDesc(desc);

        return CreateTextureImpl(desc);
    }

    public TextureView CreateTextureView(TextureViewDesc desc)
    {
        Context.Validator?.TextureViewDesc(desc);

        return CreateTextureViewImpl(desc);
    }

    public Sampler CreateSampler(SamplerDesc desc)
    {
        Context.Validator?.SamplerDesc(desc);

        return CreateSamplerImpl(desc);
    }

    public ResourceLayout CreateResourceLayout(ResourceLayoutDesc desc)
    {
        Context.Validator?.ResourceLayoutDesc(desc);

        return CreateResourceLayoutImpl(desc);
    }

    public ResourceSet CreateResourceSet(ResourceSetDesc desc)
    {
        Context.Validator?.ResourceSetDesc(desc);

        return CreateResourceSetImpl(desc);
    }

    public SwapChain CreateSwapChain(SwapChainDesc desc)
    {
        Context.Validator?.SwapChainDesc(desc);

        return CreateSwapChainImpl(desc);
    }

    public FrameBuffer CreateFrameBuffer(FrameBufferDesc desc)
    {
        Context.Validator?.FrameBufferDesc(desc);

        return CreateFrameBufferImpl(desc);
    }

    public Shader CreateShader(ShaderDesc desc)
    {
        Context.Validator?.ShaderDesc(desc);

        return CreateShaderImpl(desc);
    }

    public GraphicsPipeline CreateGraphicsPipeline(GraphicsPipelineDesc desc)
    {
        Context.Validator?.GraphicsPipelineDesc(desc);

        return CreateGraphicsPipelineImpl(desc);
    }

    public ComputePipeline CreateComputePipeline(ComputePipelineDesc desc)
    {
        Context.Validator?.ComputePipelineDesc(desc);

        return CreateComputePipelineImpl(desc);
    }

    public RayTracingPipeline CreateRayTracingPipeline(RayTracingPipelineDesc desc)
    {
        Context.Validator?.RayTracingPipelineDesc(desc);

        return CreateRayTracingPipelineImpl(desc);
    }

    protected abstract Buffer CreateBufferImpl(BufferDesc desc);

    protected abstract BufferView CreateBufferViewImpl(BufferViewDesc desc);

    protected abstract Texture CreateTextureImpl(TextureDesc desc);

    protected abstract TextureView CreateTextureViewImpl(TextureViewDesc desc);

    protected abstract Sampler CreateSamplerImpl(SamplerDesc desc);

    protected abstract ResourceLayout CreateResourceLayoutImpl(ResourceLayoutDesc desc);

    protected abstract ResourceSet CreateResourceSetImpl(ResourceSetDesc desc);

    protected abstract SwapChain CreateSwapChainImpl(SwapChainDesc desc);

    protected abstract FrameBuffer CreateFrameBufferImpl(FrameBufferDesc desc);

    protected abstract Shader CreateShaderImpl(ShaderDesc desc);

    protected abstract GraphicsPipeline CreateGraphicsPipelineImpl(GraphicsPipelineDesc desc);

    protected abstract ComputePipeline CreateComputePipelineImpl(ComputePipelineDesc desc);

    protected abstract RayTracingPipeline CreateRayTracingPipelineImpl(RayTracingPipelineDesc desc);
}
