namespace Zenith.NET;

public abstract class GraphicsContext : DisposableObject, INativeObject
{
    public const uint ConstantBufferAlignment = 256;

    protected GraphicsContext(Backend backend, bool useValidationLayer)
    {
        Backend = backend;

        Initialize(useValidationLayer,
                   out Capabilities capabilities,
                   out CommandQueue graphics,
                   out CommandQueue compute,
                   out CommandQueue copy,
                   out ValidationLayer? validationLayer);

        Capabilities = capabilities;
        Graphics = graphics;
        Compute = compute;
        Copy = copy;
        ValidationLayer = validationLayer;

        Uploader = new(this);
        Downloader = new(this);
    }

    public Backend Backend { get; }

    public Capabilities Capabilities { get; }

    public CommandQueue Graphics { get; }

    public CommandQueue Compute { get; }

    public CommandQueue Copy { get; }

    internal ValidationLayer? ValidationLayer { get; }

    internal Uploader Uploader { get; }

    internal Downloader Downloader { get; }

    public event EventHandler<ValidationMessageArgs>? ValidationMessage;

    public SwapChain CreateSwapChain(SwapChainDesc desc)
    {
        ValidationLayer?.ValidateDesc(desc);

        return CreateSwapChainImpl(desc);
    }

    public Buffer CreateBuffer(BufferDesc desc)
    {
        ValidationLayer?.ValidateDesc(desc);

        return CreateBufferImpl(desc);
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

    public ResourceTable CreateResourceTable(ResourceTableDesc desc)
    {
        ValidationLayer?.ValidateDesc(desc);

        return CreateResourceTableImpl(desc);
    }

    public Shader CreateShader(ShaderDesc desc)
    {
        ValidationLayer?.ValidateDesc(desc);

        return CreateShaderImpl(desc);
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

    public MeshShadingPipeline CreateMeshShadingPipeline(MeshShadingPipelineDesc desc)
    {
        ValidationLayer?.ValidateDesc(desc);

        return CreateMeshShadingPipelineImpl(desc);
    }

    public QueryHeap CreateQueryHeap(QueryHeapDesc desc)
    {
        ValidationLayer?.ValidateDesc(desc);

        return CreateQueryHeapImpl(desc);
    }

    public abstract nint GetNativeObject(NativeObjectType type);

    protected override void Destroy()
    {
        Graphics.Dispose();
        Compute.Dispose();
        Copy.Dispose();
        ValidationLayer?.Dispose();

        Uploader.Dispose();
        Downloader.Dispose();
    }

    protected abstract void Initialize(bool useValidationLayer,
                                       out Capabilities capabilities,
                                       out CommandQueue graphics,
                                       out CommandQueue compute,
                                       out CommandQueue copy,
                                       out ValidationLayer? validationLayer);

    protected abstract SwapChain CreateSwapChainImpl(SwapChainDesc desc);

    protected abstract Buffer CreateBufferImpl(BufferDesc desc);

    protected abstract Texture CreateTextureImpl(TextureDesc desc);

    protected abstract TextureView CreateTextureViewImpl(TextureViewDesc desc);

    protected abstract Sampler CreateSamplerImpl(SamplerDesc desc);

    protected abstract ResourceTable CreateResourceTableImpl(ResourceTableDesc desc);

    protected abstract Shader CreateShaderImpl(ShaderDesc desc);

    protected abstract GraphicsPipeline CreateGraphicsPipelineImpl(GraphicsPipelineDesc desc);

    protected abstract ComputePipeline CreateComputePipelineImpl(ComputePipelineDesc desc);

    protected abstract MeshShadingPipeline CreateMeshShadingPipelineImpl(MeshShadingPipelineDesc desc);

    protected abstract QueryHeap CreateQueryHeapImpl(QueryHeapDesc desc);

    internal void OnValidationMessage(ValidationMessageArgs args)
    {
        ValidationMessage?.Invoke(this, args);
    }
}
