namespace Zenith.NET;

public abstract class ResourceFactory(GraphicsContext context)
{
    public GraphicsContext Context { get; } = context;

    public abstract SwapChain CreateSwapChain(SwapChainDesc desc);

    public abstract FrameBuffer CreateFrameBuffer(FrameBufferDesc desc);

    public abstract Buffer CreateBuffer(BufferDesc desc);

    public abstract Texture CreateTexture(TextureDesc desc);

    public abstract Sampler CreateSampler(SamplerDesc desc);

    public abstract Shader CreateShader(ShaderDesc desc);

    public abstract ResourceLayout CreateResourceLayout(ResourceLayoutDesc desc);

    public abstract ResourceSet CreateResourceSet(ResourceSetDesc desc);

    public abstract CommandQueue CreateCommandQueue(CommandQueueType type);
}
