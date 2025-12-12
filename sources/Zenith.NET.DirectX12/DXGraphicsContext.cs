using Silk.NET.Core.Native;
using Silk.NET.Direct3D12;
using Silk.NET.DXGI;

namespace Zenith.NET.DirectX12;

internal unsafe class DXGraphicsContext(bool useValidationLayer) : GraphicsContext(Backend.DirectX12, useValidationLayer)
{
    public ComPtr<IDXGIFactory7> Factory7;

    public ComPtr<IDXGIAdapter4> Adapter4;

    public ComPtr<ID3D12Device> Device;

    public ComPtr<ID3D12CommandQueue> GraphicsQueue;

    public ComPtr<ID3D12CommandQueue> ComputeQueue;

    public ComPtr<ID3D12CommandQueue> CopyQueue;

    public ComPtr<ID3D12CommandSignature> DrawSignature;

    public ComPtr<ID3D12CommandSignature> DrawIndexedSignature;

    public ComPtr<ID3D12CommandSignature> DispatchSignature;

    public ComPtr<ID3D12CommandSignature> DispatchMeshSignature;

    public DXGI DXGI { get; } = DXGI.GetApi(null);

    public D3D12 D3D12 { get; } = D3D12.GetApi();

    public DXDescriptorAllocator RtvAllocator => field ??= new(this, DescriptorHeapType.Rtv);

    public DXDescriptorAllocator DsvAllocator => field ??= new(this, DescriptorHeapType.Dsv);

    public DXDescriptorAllocator CbvSrvUavAllocator => field ??= new(this, DescriptorHeapType.CbvSrvUav);

    public DXDescriptorAllocator SamplerAllocator => field ??= new(this, DescriptorHeapType.Sampler);

    protected override void Initialize(bool useValidationLayer,
                                       out Capabilities capabilities,
                                       out CommandQueue graphics,
                                       out CommandQueue compute,
                                       out CommandQueue copy,
                                       out ValidationLayer? validationLayer)
    {
        if (useValidationLayer)
        {
            D3D12.GetDebugInterface(out ComPtr<ID3D12Debug> debug).Success();

            debug.EnableDebugLayer();

            debug.Dispose();
        }

        DXGI.CreateDXGIFactory2(useValidationLayer ? DXGI.CreateFactoryDebug : 0u, out Factory7).Success();

        Factory7.EnumAdapterByGpuPreference(0, GpuPreference.HighPerformance, out Adapter4).Success();

        D3D12.CreateDevice(Adapter4, D3DFeatureLevel.Level120, out Device).Success();

        CommandQueueDesc commandQueueDesc = new() { Type = CommandListType.Direct };
        Device.CreateCommandQueue(&commandQueueDesc, out GraphicsQueue).Success();

        commandQueueDesc.Type = CommandListType.Compute;
        Device.CreateCommandQueue(&commandQueueDesc, out ComputeQueue).Success();

        commandQueueDesc.Type = CommandListType.Copy;
        Device.CreateCommandQueue(&commandQueueDesc, out CopyQueue).Success();

        IndirectArgumentDesc indirectArgumentDesc = new() { Type = IndirectArgumentType.Draw };
        CommandSignatureDesc commandSignatureDesc = new() { ByteStride = (uint)sizeof(IndirectDrawArgs), NumArgumentDescs = 1, PArgumentDescs = &indirectArgumentDesc };
        Device.CreateCommandSignature(&commandSignatureDesc, (ComPtr<ID3D12RootSignature>)null, out DrawSignature).Success();

        indirectArgumentDesc.Type = IndirectArgumentType.DrawIndexed;
        commandSignatureDesc.ByteStride = (uint)sizeof(IndirectDrawIndexedArgs);
        Device.CreateCommandSignature(&commandSignatureDesc, (ComPtr<ID3D12RootSignature>)null, out DrawIndexedSignature).Success();

        indirectArgumentDesc.Type = IndirectArgumentType.Dispatch;
        commandSignatureDesc.ByteStride = (uint)sizeof(IndirectDispatchArgs);
        Device.CreateCommandSignature(&commandSignatureDesc, (ComPtr<ID3D12RootSignature>)null, out DispatchSignature).Success();

        indirectArgumentDesc.Type = IndirectArgumentType.DispatchMesh;
        commandSignatureDesc.ByteStride = (uint)sizeof(IndirectDispatchArgs);
        Device.CreateCommandSignature(&commandSignatureDesc, (ComPtr<ID3D12RootSignature>)null, out DispatchMeshSignature).Success();

        capabilities = new DXCapabilities(this);
        //graphics = new VKCommandQueue(this, CommandQueueType.Graphics, GraphicsQueue, QueueFamilyIndices[0]);
        //compute = new VKCommandQueue(this, CommandQueueType.Compute, ComputeQueue, QueueFamilyIndices.Length > 1 ? QueueFamilyIndices[1] : QueueFamilyIndices[0]);
        //copy = new VKCommandQueue(this, CommandQueueType.Copy, CopyQueue, QueueFamilyIndices.Length > 2 ? QueueFamilyIndices[2] : QueueFamilyIndices[0]);
        //validationLayer = useValidationLayer ? new VKValidationLayer(this) : null;

        throw new NotImplementedException();
    }

    protected override SwapChain CreateSwapChainImpl(SwapChainDesc desc)
    {
        throw new NotImplementedException();
    }

    protected override FrameBuffer CreateFrameBufferImpl(FrameBufferDesc desc)
    {
        throw new NotImplementedException();
    }

    protected override Shader CreateShaderImpl(ShaderDesc desc)
    {
        throw new NotImplementedException();
    }

    protected override Buffer CreateBufferImpl(BufferDesc desc)
    {
        throw new NotImplementedException();
    }

    protected override BufferView CreateBufferViewImpl(BufferViewDesc desc)
    {
        throw new NotImplementedException();
    }

    protected override Texture CreateTextureImpl(TextureDesc desc)
    {
        throw new NotImplementedException();
    }

    protected override TextureView CreateTextureViewImpl(TextureViewDesc desc)
    {
        throw new NotImplementedException();
    }

    protected override Sampler CreateSamplerImpl(SamplerDesc desc)
    {
        throw new NotImplementedException();
    }

    protected override ResourceLayout CreateResourceLayoutImpl(ResourceLayoutDesc desc)
    {
        throw new NotImplementedException();
    }

    protected override ResourceSet CreateResourceSetImpl(ResourceSetDesc desc)
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

    protected override RayTracingPipeline CreateRayTracingPipelineImpl(RayTracingPipelineDesc desc)
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
}
