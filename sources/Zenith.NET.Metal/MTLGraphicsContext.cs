using Metal.NET;

namespace Zenith.NET.Metal;

internal class MTLGraphicsContext(bool useValidationLayer) : GraphicsContext(Backend.Metal, useValidationLayer)
{
    public MTLDevice Device = MTLDevice.Null;

    public MTL4Compiler Compiler = MTL4Compiler.Null;

    public MTLResidencySet ResidencySet = MTLResidencySet.Null;

    public MTL4CommandQueue GraphicsQueue = MTL4CommandQueue.Null;

    public MTL4CommandQueue ComputeQueue = MTL4CommandQueue.Null;

    public MTL4CommandQueue CopyQueue = MTL4CommandQueue.Null;

    public void AddAllocation(MTLAllocation allocation)
    {
        ResidencySet.AddAllocation(allocation);
        ResidencySet.Commit();
    }

    public void RemoveAllocation(MTLAllocation allocation)
    {
        ResidencySet.RemoveAllocation(allocation);
        ResidencySet.Commit();
    }

    protected override void Initialize(bool useValidationLayer,
                                       out Capabilities capabilities,
                                       out CommandQueue graphics,
                                       out CommandQueue compute,
                                       out CommandQueue copy,
                                       out ValidationLayer? validationLayer)
    {
        Device = MTLDevice.CreateSystemDefaultDevice();

        if (!Device.SupportsFamily(MTLGPUFamily.Metal4))
        {
            throw new NotSupportedException("Metal 4 is not supported on system default device.");
        }

        Compiler = Device.MakeCompiler(new(), out NSError error);
        error.Success();

        ResidencySet = Device.MakeResidencySet(new(), out error);
        error.Success();

        GraphicsQueue = Device.MakeMTL4CommandQueue();
        ComputeQueue = Device.MakeMTL4CommandQueue();
        CopyQueue = Device.MakeMTL4CommandQueue();

        GraphicsQueue.AddResidencySet(ResidencySet);
        ComputeQueue.AddResidencySet(ResidencySet);
        CopyQueue.AddResidencySet(ResidencySet);

        capabilities = new MTLCapabilities(this);
        graphics = new MTLCommandQueue(this, CommandQueueType.Graphics, GraphicsQueue);
        compute = new MTLCommandQueue(this, CommandQueueType.Compute, ComputeQueue);
        copy = new MTLCommandQueue(this, CommandQueueType.Copy, CopyQueue);
        validationLayer = null;
    }

    protected override SwapChain CreateSwapChainImpl(SwapChainDesc desc)
    {
        return new MTLSwapChain(this, desc);
    }

    protected override FrameBuffer CreateFrameBufferImpl(FrameBufferDesc desc)
    {
        return new MTLFrameBuffer(this, desc);
    }

    protected override Shader CreateShaderImpl(ShaderDesc desc)
    {
        return new MTLShader(this, desc);
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
        return new MTLSampler(this, desc);
    }

    protected override ResourceLayout CreateResourceLayoutImpl(ResourceLayoutDesc desc)
    {
        return new MTLResourceLayout(this, desc);
    }

    protected override ResourceTable CreateResourceTableImpl(ResourceTableDesc desc)
    {
        return new MTLResourceTable(this, desc);
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
        throw new NotImplementedException();
    }

    protected override QueryHeap CreateQueryHeapImpl(QueryHeapDesc desc)
    {
        throw new NotImplementedException();
    }

    protected override void Destroy()
    {
        base.Destroy();

        CopyQueue.RemoveResidencySet(ResidencySet);
        ComputeQueue.RemoveResidencySet(ResidencySet);
        GraphicsQueue.RemoveResidencySet(ResidencySet);

        CopyQueue.Dispose();
        ComputeQueue.Dispose();
        GraphicsQueue.Dispose();

        ResidencySet.Dispose();
        Compiler.Dispose();

        Device.Dispose();
    }
}
