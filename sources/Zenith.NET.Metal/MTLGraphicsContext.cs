using Metal.NET;

namespace Zenith.NET.Metal;

internal class MTLGraphicsContext(bool useValidationLayer) : GraphicsContext(GraphicsApi.Metal, useValidationLayer)
{
    private readonly Lock @lock = new();

    public MTLDevice Device = MTLDevice.CreateSystemDefaultDevice();

    public MTL4Compiler Compiler = MTL4Compiler.Null;

    public MTLResidencySet ResidencySet = MTLResidencySet.Null;

    public void Register(MTLAllocation allocation)
    {
        using Lock.Scope _ = @lock.EnterScope();

        ResidencySet.AddAllocation(allocation);
        ResidencySet.Commit();
    }

    public void Unregister(MTLAllocation allocation)
    {
        using Lock.Scope _ = @lock.EnterScope();

        ResidencySet.RemoveAllocation(allocation);
        ResidencySet.Commit();
    }

    public override nint GetNativeObject(NativeObjectType type)
    {
        return 0;
    }

    protected override void Initialize(bool useValidationLayer,
                                       out Capabilities capabilities,
                                       out CommandQueue graphicsQueue,
                                       out CommandQueue computeQueue,
                                       out CommandQueue copyQueue,
                                       out ValidationLayer? validationLayer)
    {
        if (!Device.SupportsFamily(MTLGPUFamily.Metal4))
        {
            throw new NotSupportedException("Metal 4 is required but not supported by this device.");
        }

        Compiler = Device.MakeCompiler(new(), out NSError error);
        error.Success();

        ResidencySet = Device.MakeResidencySet(new(), out error);
        error.Success();

        capabilities = new MTLCapabilities(this);
        graphicsQueue = new MTLCommandQueue(this, CommandQueueType.Graphics);
        computeQueue = new MTLCommandQueue(this, CommandQueueType.Compute);
        copyQueue = new MTLCommandQueue(this, CommandQueueType.Copy);
        validationLayer = null;
    }

    protected override SwapChain CreateSwapChainImpl(SwapChainDesc desc)
    {
        return new MTLSwapChain(this, desc);
    }

    protected override Heap CreateHeapImpl(HeapDesc desc)
    {
        return new MTLHeap(this, desc);
    }

    protected override SizeAndAlignment GetSizeAndAlignmentImpl(BufferDesc desc)
    {
        MTLSizeAndAlign sizeAndAlign = Device.HeapBufferSizeAndAlign(desc.SizeInBytes, MTLFormats.Metal(desc.Residency));

        return new(sizeAndAlign.Size, sizeAndAlign.Align);
    }

    protected override SizeAndAlignment GetSizeAndAlignmentImpl(TextureDesc desc)
    {
        MTLSizeAndAlign sizeAndAlign = Device.HeapTextureSizeAndAlign(MTLTexture.Descriptor(desc));

        return new(sizeAndAlign.Size, sizeAndAlign.Align);
    }

    protected override Buffer CreateBufferImpl(BufferDesc desc)
    {
        return new MTLBuffer(this, desc);
    }

    protected override BufferView CreateBufferViewImpl(BufferViewDesc desc)
    {
        return new MTLBufferView(this, desc);
    }

    protected override Texture CreateTextureImpl(TextureDesc desc)
    {
        return new MTLTexture(this, desc);
    }

    protected override Texture CreateTextureImpl(TextureDesc desc, NativeTextureType nativeTextureType, nint nativeTexture)
    {
        return new MTLTexture(this, desc, nativeTextureType switch
        {
            NativeTextureType.MTLSharedTextureHandle => Device.MakeSharedTexture(new MTLSharedTextureHandle(nativeTexture, NativeObjectOwnership.Borrowed)),
            NativeTextureType.IOSurfaceRef => Device.MakeTexture(MTLTexture.Descriptor(desc), nativeTexture, 0),
            _ => MtlTexture.Null
        });
    }

    protected override TextureView CreateTextureViewImpl(TextureViewDesc desc)
    {
        return new MTLTextureView(this, desc);
    }

    protected override Sampler CreateSamplerImpl(SamplerDesc desc)
    {
        return new MTLSampler(this, desc);
    }

    protected override Shader CreateShaderImpl(ShaderDesc desc)
    {
        return new MTLShader(this, desc);
    }

    protected override GraphicsPipeline CreateGraphicsPipelineImpl(GraphicsPipelineDesc desc)
    {
        return new MTLGraphicsPipeline(this, desc);
    }

    protected override ComputePipeline CreateComputePipelineImpl(ComputePipelineDesc desc)
    {
        return new MTLComputePipeline(this, desc);
    }

    protected override MeshShadingPipeline CreateMeshShadingPipelineImpl(MeshShadingPipelineDesc desc)
    {
        return new MTLMeshShadingPipeline(this, desc);
    }

    protected override QueryHeap CreateQueryHeapImpl(QueryHeapDesc desc)
    {
        return new MTLQueryHeap(this, desc);
    }

    protected override void Destroy()
    {
        base.Destroy();

        ResidencySet.Dispose();
        Compiler.Dispose();
        Device.Dispose();
    }
}
