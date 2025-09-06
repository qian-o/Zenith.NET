namespace Zenith.NET;

public abstract class GraphicsContext : DisposableObject
{
    protected GraphicsContext(Backend backend, bool useValidationLayer)
    {
        Backend = backend;

        Initialize(useValidationLayer,
                   out Capabilities capabilities,
                   out ValidationLayer? validationLayer,
                   out CommandQueue direct,
                   out CommandQueue compute,
                   out CommandQueue copy);

        Capabilities = capabilities;
        ValidationLayer = validationLayer;
        Direct = direct;
        Compute = compute;
        Copy = copy;

        Uploader = new(this);
    }

    public Backend Backend { get; }

    public Capabilities Capabilities { get; }

    public ValidationLayer? ValidationLayer { get; }

    public CommandQueue Direct { get; }

    public CommandQueue Compute { get; }

    public CommandQueue Copy { get; }

    internal Uploader Uploader { get; }

    public SwapChain CreateSwapChain(SwapChainDesc desc)
    {
        ValidationLayer?.ValidateDesc(desc);

        return CreateSwapChainImpl(desc);
    }

    public FrameBuffer CreateFrameBuffer(FrameBufferDesc desc)
    {
        ValidationLayer?.ValidateDesc(desc);

        return CreateFrameBufferImpl(desc);
    }

    public Shader CreateShader(ShaderDesc desc)
    {
        ValidationLayer?.ValidateDesc(desc);

        return CreateShaderImpl(desc);
    }

    public Buffer CreateBuffer(BufferDesc desc)
    {
        ValidationLayer?.ValidateDesc(desc);

        return CreateBufferImpl(desc);
    }

    public BufferView CreateBufferView(BufferViewDesc desc)
    {
        ValidationLayer?.ValidateDesc(desc);

        return CreateBufferViewImpl(desc);
    }

    public Texture CreateTexture(TextureDesc desc)
    {
        ValidationLayer?.ValidateDesc(desc);

        return CreateTextureImpl(desc);
    }

    public TextureView CreateTextureView(TextureViewDesc desc)
    {
        ValidationLayer?.ValidateDesc(desc);

        return CreateTextureViewImpl(desc);
    }

    public Sampler CreateSampler(SamplerDesc desc)
    {
        ValidationLayer?.ValidateDesc(desc);

        return CreateSamplerImpl(desc);
    }

    public ResourceLayout CreateResourceLayout(ResourceLayoutDesc desc)
    {
        ValidationLayer?.ValidateDesc(desc);

        return CreateResourceLayoutImpl(desc);
    }

    public ResourceSet CreateResourceSet(ResourceSetDesc desc)
    {
        ValidationLayer?.ValidateDesc(desc);

        return CreateResourceSetImpl(desc);
    }

    public GraphicsPipeline CreateGraphicsPipeline(GraphicsPipelineDesc desc)
    {
        ValidationLayer?.ValidateDesc(desc);

        return CreateGraphicsPipelineImpl(desc);
    }

    public ComputePipeline CreateComputePipeline(ComputePipelineDesc desc)
    {
        ValidationLayer?.ValidateDesc(desc);

        return CreateComputePipelineImpl(desc);
    }

    public RayTracingPipeline CreateRayTracingPipeline(RayTracingPipelineDesc desc)
    {
        ValidationLayer?.ValidateDesc(desc);

        return CreateRayTracingPipelineImpl(desc);
    }

    public MeshShadingPipeline CreateMeshShadingPipeline(MeshShadingPipelineDesc desc)
    {
        ValidationLayer?.ValidateDesc(desc);

        return CreateMeshShadingPipelineImpl(desc);
    }

    protected override void Destroy()
    {
        ValidationLayer?.Dispose();
        Direct.Dispose();
        Compute.Dispose();
        Copy.Dispose();

        Uploader.Dispose();
    }

    protected abstract void Initialize(bool useValidationLayer,
                                       out Capabilities capabilities,
                                       out ValidationLayer? validationLayer,
                                       out CommandQueue direct,
                                       out CommandQueue compute,
                                       out CommandQueue copy);

    protected abstract SwapChain CreateSwapChainImpl(SwapChainDesc desc);

    protected abstract FrameBuffer CreateFrameBufferImpl(FrameBufferDesc desc);

    protected abstract Shader CreateShaderImpl(ShaderDesc desc);

    protected abstract Buffer CreateBufferImpl(BufferDesc desc);

    protected abstract BufferView CreateBufferViewImpl(BufferViewDesc desc);

    protected abstract Texture CreateTextureImpl(TextureDesc desc);

    protected abstract TextureView CreateTextureViewImpl(TextureViewDesc desc);

    protected abstract Sampler CreateSamplerImpl(SamplerDesc desc);

    protected abstract ResourceLayout CreateResourceLayoutImpl(ResourceLayoutDesc desc);

    protected abstract ResourceSet CreateResourceSetImpl(ResourceSetDesc desc);

    protected abstract GraphicsPipeline CreateGraphicsPipelineImpl(GraphicsPipelineDesc desc);

    protected abstract ComputePipeline CreateComputePipelineImpl(ComputePipelineDesc desc);

    protected abstract RayTracingPipeline CreateRayTracingPipelineImpl(RayTracingPipelineDesc desc);

    protected abstract MeshShadingPipeline CreateMeshShadingPipelineImpl(MeshShadingPipelineDesc desc);
}
