namespace Zenith.NET;

public abstract class GraphicsContext : DisposableObject
{
    protected GraphicsContext(Backend backend, bool useDebugLayer)
    {
        Backend = backend;

        Initialize(useDebugLayer,
                   out Capabilities capabilities,
                   out CommandQueue direct,
                   out CommandQueue compute,
                   out CommandQueue copy);

        Capabilities = capabilities;
        Direct = direct;
        Compute = compute;
        Copy = copy;

        Uploader = new(this);
    }

    public Backend Backend { get; }

    public Capabilities Capabilities { get; }

    public CommandQueue Direct { get; }

    public CommandQueue Compute { get; }

    public CommandQueue Copy { get; }

    internal Uploader Uploader { get; }

    public event EventHandler<DebugCallbackArgs>? DebugCallback;

    public abstract SwapChain CreateSwapChain(SwapChainDesc desc);

    public abstract FrameBuffer CreateFrameBuffer(FrameBufferDesc desc);

    public abstract Shader CreateShader(ShaderDesc desc);

    public abstract Buffer CreateBuffer(BufferDesc desc);

    public abstract BufferView CreateBufferView(BufferViewDesc desc);

    public abstract Texture CreateTexture(TextureDesc desc);

    public abstract TextureView CreateTextureView(TextureViewDesc desc);

    public abstract Sampler CreateSampler(SamplerDesc desc);

    public abstract ResourceLayout CreateResourceLayout(ResourceLayoutDesc desc);

    public abstract ResourceSet CreateResourceSet(ResourceSetDesc desc);

    public abstract GraphicsPipeline CreateGraphicsPipeline(GraphicsPipelineDesc desc);

    public abstract ComputePipeline CreateComputePipeline(ComputePipelineDesc desc);

    public abstract RayTracingPipeline CreateRayTracingPipeline(RayTracingPipelineDesc desc);

    protected void PublishDebugCallback(MessageSeverity severity, string message)
    {
        DebugCallback?.Invoke(this, new(severity, message));
    }

    protected override void Destroy()
    {
        Direct.Dispose();
        Compute.Dispose();
        Copy.Dispose();

        Uploader.Dispose();
    }

    protected abstract void Initialize(bool useDebugLayer,
                                       out Capabilities capabilities,
                                       out CommandQueue direct,
                                       out CommandQueue compute,
                                       out CommandQueue copy);
}
