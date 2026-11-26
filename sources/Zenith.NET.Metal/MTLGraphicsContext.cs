using Metal.NET;

namespace Zenith.NET.Metal;

internal class MTLGraphicsContext(bool useValidationLayer) : GraphicsContext(GraphicsApi.Metal, useValidationLayer)
{
    private readonly Lock @lock = new();

    public MTLDevice Device;

    public MTL4Compiler Compiler;

    public MTLResidencySet ResidencySet;

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
        return type switch
        {
            NativeObjectType.MTLDevice => Device.NativePtr,
            _ => default
        };
    }

    protected override void Initialize(bool useValidationLayer,
                                       out Capabilities capabilities,
                                       out CommandQueue graphicsQueue,
                                       out CommandQueue computeQueue,
                                       out CommandQueue transferQueue,
                                       out CommandQueue presentQueue,
                                       out ValidationLayer? validationLayer)
    {
        Device = MTLDevice.CreateSystemDefaultDevice();

        if (!Device.SupportsFamily(MTLGPUFamily.Metal4))
        {
            throw new NotSupportedException("This device does not support Metal 4.0 or higher.");
        }

        using MTL4CompilerDescriptor compilerDescriptor = new();
        Compiler = Device.MakeCompiler(compilerDescriptor, out NSError error);
        error.Success();

        using MTLResidencySetDescriptor residencySetDescriptor = new();
        ResidencySet = Device.MakeResidencySet(residencySetDescriptor, out error);
        error.Success();

        capabilities = new MTLCapabilities(this);
        graphicsQueue = new MTLCommandQueue(this, CommandQueueType.Graphics);
        computeQueue = new MTLCommandQueue(this, CommandQueueType.Compute);
        transferQueue = new MTLCommandQueue(this, CommandQueueType.Transfer);
        presentQueue = new MTLCommandQueue(this, CommandQueueType.Present);
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
        using MTLTextureDescriptor descriptor = MTLTexture.Descriptor(desc);

        MTLSizeAndAlign sizeAndAlign = Device.HeapTextureSizeAndAlign(descriptor);

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
        using MTLTextureDescriptor descriptor = MTLTexture.Descriptor(desc);

        return new MTLTexture(this, desc, nativeTextureType switch
        {
            NativeTextureType.MTLSharedTextureHandle => Device.MakeSharedTexture(new MTLSharedTextureHandle(nativeTexture)),
            NativeTextureType.IOSurfaceRef => Device.MakeTexture(descriptor, nativeTexture, 0),
            _ => default
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
