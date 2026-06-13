using Metal.NET;

namespace Zenith.NET.Metal;

internal class MTLGraphicsContext(bool useValidationLayer) : GraphicsContext(GraphicsApi.Metal, useValidationLayer)
{
    public MTLDevice Device = MTLDevice.CreateSystemDefaultDevice();

    public MTL4Compiler Compiler = MTL4Compiler.Null;

    public MTLResidencySet ResidencySet = MTLResidencySet.Null;

    public MTL4CommandQueue GraphicsCommandQueue = MTL4CommandQueue.Null;

    public MTL4CommandQueue ComputeCommandQueue = MTL4CommandQueue.Null;

    public MTL4CommandQueue CopyCommandQueue = MTL4CommandQueue.Null;

    public void AddResidency(MTLAllocation allocation)
    {
        ResidencySet.AddAllocation(allocation);
        ResidencySet.Commit();
    }

    public void RemoveResidency(MTLAllocation allocation)
    {
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

        GraphicsCommandQueue = Device.MakeMTL4CommandQueue();
        GraphicsCommandQueue.AddResidencySet(ResidencySet);

        ComputeCommandQueue = Device.MakeMTL4CommandQueue();
        ComputeCommandQueue.AddResidencySet(ResidencySet);

        CopyCommandQueue = Device.MakeMTL4CommandQueue();
        CopyCommandQueue.AddResidencySet(ResidencySet);

        capabilities = new MTLCapabilities(this);
        graphicsQueue = new MTLCommandQueue(this, CommandQueueType.Graphics, GraphicsCommandQueue);
        computeQueue = new MTLCommandQueue(this, CommandQueueType.Compute, ComputeCommandQueue);
        copyQueue = new MTLCommandQueue(this, CommandQueueType.Copy, CopyCommandQueue);
        validationLayer = null;
    }

    protected override SwapChain CreateSwapChainImpl(SwapChainDesc desc)
    {
        throw new NotImplementedException();
    }

    protected override Heap CreateHeapImpl(HeapDesc desc)
    {
        throw new NotImplementedException();
    }

    protected override SizeAndAlignment GetSizeAndAlignmentImpl(BufferDesc desc)
    {
        throw new NotImplementedException();
    }

    protected override SizeAndAlignment GetSizeAndAlignmentImpl(TextureDesc desc)
    {
        throw new NotImplementedException();
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

    protected override TextureView CreateTextureViewImpl(TextureViewDesc desc)
    {
        return new MTLTextureView(this, desc);
    }

    protected override Sampler CreateSamplerImpl(SamplerDesc desc)
    {
        throw new NotImplementedException();
    }

    protected override Shader CreateShaderImpl(ShaderDesc desc)
    {
        throw new NotImplementedException();
    }

    protected override GraphicsPipeline CreateGraphicsPipelineImpl(GraphicsPipelineDesc desc)
    {
        throw new NotImplementedException();
    }

    protected override ComputePipeline CreateComputePipelineImpl(ComputePipelineDesc desc)
    {
        throw new NotImplementedException();
    }

    protected override MeshShadingPipeline CreateMeshShadingPipelineImpl(MeshShadingPipelineDesc desc)
    {
        throw new NotImplementedException();
    }

    protected override QueryHeap CreateQueryHeapImpl(QueryHeapDesc desc)
    {
        throw new NotImplementedException();
    }

    protected override void Destroy()
    {
        base.Destroy();

        CopyCommandQueue.Dispose();
        ComputeCommandQueue.Dispose();
        GraphicsCommandQueue.Dispose();

        ResidencySet.Dispose();
        Compiler.Dispose();
        Device.Dispose();
    }
}
