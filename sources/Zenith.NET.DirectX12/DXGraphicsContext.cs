using Silk.NET.Core.Native;
using Silk.NET.Direct3D12;
using Silk.NET.DXGI;

namespace Zenith.NET.DirectX12;

internal unsafe class DXGraphicsContext(bool useValidationLayer) : GraphicsContext(GraphicsApi.DirectX12, useValidationLayer)
{
    public const ulong DefaultHeapAlignment = 4194304;

    public const uint Shader4ComponentMapping = 0x1688;

    public ComPtr<IDXGIFactory7> Factory7;

    public ComPtr<IDXGIAdapter4> Adapter4;

    public ComPtr<ID3D12Device10> Device10;

    public ComPtr<ID3D12InfoQueue1>? InfoQueue1;

    public ComPtr<ID3D12RootSignature> RootSignature;

    public ComPtr<ID3D12CommandSignature> DrawSignature;

    public ComPtr<ID3D12CommandSignature> DrawIndexedSignature;

    public ComPtr<ID3D12CommandSignature> DispatchSignature;

    public ComPtr<ID3D12CommandSignature> DispatchMeshSignature;

    public ComPtr<ID3D12CommandQueue> GraphicsCommandQueue;

    public ComPtr<ID3D12CommandQueue> ComputeCommandQueue;

    public ComPtr<ID3D12CommandQueue> CopyCommandQueue;

    public DXGI DXGI { get; } = DXGI.GetApi(null);

    public D3D12 D3D12 { get; } = D3D12.GetApi();

    public DXDescriptorHeap RtvHeap => field ??= new(this, DescriptorHeapType.Rtv, 1024, false);

    public DXDescriptorHeap DsvHeap => field ??= new(this, DescriptorHeapType.Dsv, 256, false);

    public DXDescriptorHeap CbvSrvUavHeap => field ??= new(this, DescriptorHeapType.CbvSrvUav, 1000000, true);

    public DXDescriptorHeap SamplerHeap => field ??= new(this, DescriptorHeapType.Sampler, 2048, true);

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
        if (useValidationLayer && D3D12.GetDebugInterface(out ComPtr<ID3D12Debug> debug).IsSuccess())
        {
            debug.EnableDebugLayer();

            debug.Dispose();
        }

        DXGI.CreateDXGIFactory2(Convert.ToUInt32(useValidationLayer), out Factory7).Success();

        Factory7.EnumAdapterByGpuPreference(0, GpuPreference.HighPerformance, out Adapter4).Success();

        if (!D3D12.CreateDevice(Adapter4, D3DFeatureLevel.Level122, out Device10).IsSuccess())
        {
            throw new NotSupportedException("Direct3D 12 Feature Level 12_2 is not supported on the selected adapter.");
        }

        if (Device10.QueryInterface(out ComPtr<ID3D12InfoQueue1> infoQueue1).IsSuccess())
        {
            InfoQueue1 = infoQueue1;
        }

        RootParameter1 rootParameter = new()
        {
            ParameterType = RootParameterType.TypeCbv,
            ShaderVisibility = ShaderVisibility.All,
            Descriptor = new() { Flags = RootDescriptorFlags.DataVolatile }
        };

        RootSignatureDesc1 rootSignatureDesc = new()
        {
            NumParameters = 1,
            PParameters = &rootParameter,
            Flags = RootSignatureFlags.AllowInputAssemblerInputLayout | RootSignatureFlags.CbvSrvUavHeapDirectlyIndexed | RootSignatureFlags.SamplerHeapDirectlyIndexed
        };

        VersionedRootSignatureDesc versionedRootSignatureDesc = new()
        {
            Version = D3DRootSignatureVersion.Version11,
            Desc11 = rootSignatureDesc
        };

        ComPtr<ID3D10Blob> rootSignatureBlob = default;
        ComPtr<ID3D10Blob> rootSignatureError = default;
        D3D12.SerializeVersionedRootSignature(&versionedRootSignatureDesc, ref rootSignatureBlob, ref rootSignatureError).Success();
        Device10.CreateRootSignature(0, rootSignatureBlob.GetBufferPointer(), rootSignatureBlob.GetBufferSize(), out RootSignature).Success();
        rootSignatureBlob.Dispose();
        rootSignatureError.Dispose();

        IndirectArgumentDesc indirectArgumentDesc = new() { Type = IndirectArgumentType.Draw };
        CommandSignatureDesc commandSignatureDesc = new() { ByteStride = (uint)sizeof(IndirectDrawArgs), NumArgumentDescs = 1, PArgumentDescs = &indirectArgumentDesc };
        Device10.CreateCommandSignature(&commandSignatureDesc, default(ComPtr<ID3D12RootSignature>), out DrawSignature).Success();

        indirectArgumentDesc.Type = IndirectArgumentType.DrawIndexed;
        commandSignatureDesc.ByteStride = (uint)sizeof(IndirectDrawIndexedArgs);
        Device10.CreateCommandSignature(&commandSignatureDesc, default(ComPtr<ID3D12RootSignature>), out DrawIndexedSignature).Success();

        indirectArgumentDesc.Type = IndirectArgumentType.Dispatch;
        commandSignatureDesc.ByteStride = (uint)sizeof(IndirectDispatchArgs);
        Device10.CreateCommandSignature(&commandSignatureDesc, default(ComPtr<ID3D12RootSignature>), out DispatchSignature).Success();

        indirectArgumentDesc.Type = IndirectArgumentType.DispatchMesh;
        commandSignatureDesc.ByteStride = (uint)sizeof(IndirectDispatchMeshArgs);
        Device10.CreateCommandSignature(&commandSignatureDesc, default(ComPtr<ID3D12RootSignature>), out DispatchMeshSignature).Success();

        CommandQueueDesc commandQueueDesc = new() { Type = CommandListType.Direct };
        Device10.CreateCommandQueue(&commandQueueDesc, out GraphicsCommandQueue).Success();

        commandQueueDesc.Type = CommandListType.Compute;
        Device10.CreateCommandQueue(&commandQueueDesc, out ComputeCommandQueue).Success();

        commandQueueDesc.Type = CommandListType.Copy;
        Device10.CreateCommandQueue(&commandQueueDesc, out CopyCommandQueue).Success();

        capabilities = new DXCapabilities(this);
        graphicsQueue = new DXCommandQueue(this, CommandQueueType.Graphics, GraphicsCommandQueue);
        computeQueue = new DXCommandQueue(this, CommandQueueType.Compute, ComputeCommandQueue);
        copyQueue = new DXCommandQueue(this, CommandQueueType.Copy, CopyCommandQueue);
        validationLayer = useValidationLayer ? new DXValidationLayer(this) : null;
    }

    protected override SwapChain CreateSwapChainImpl(SwapChainDesc desc)
    {
        throw new NotImplementedException();
    }

    protected override Heap CreateHeapImpl(HeapDesc desc)
    {
        return new DXHeap(this, desc);
    }

    protected override SizeAndAlignment GetSizeAndAlignmentImpl(BufferDesc desc)
    {
        ResourceDesc1 resourceDesc = DXBuffer.ResourceDesc(desc);

        ResourceAllocationInfo info = Device10.GetResourceAllocationInfo2(0, 1, &resourceDesc, default(Span<ResourceAllocationInfo1>));

        return new(info.SizeInBytes, info.Alignment);
    }

    protected override SizeAndAlignment GetSizeAndAlignmentImpl(TextureDesc desc)
    {
        ResourceDesc1 resourceDesc = DXTexture.ResourceDesc(desc);

        ResourceAllocationInfo info = Device10.GetResourceAllocationInfo2(0, 1, &resourceDesc, default(Span<ResourceAllocationInfo1>));

        return new(info.SizeInBytes, info.Alignment);
    }

    protected override Buffer CreateBufferImpl(BufferDesc desc)
    {
        return new DXBuffer(this, desc, null);
    }

    protected override BufferView CreateBufferViewImpl(BufferViewDesc desc)
    {
        return new DXBufferView(this, desc);
    }

    protected override Texture CreateTextureImpl(TextureDesc desc)
    {
        return new DXTexture(this, desc, null);
    }

    protected override TextureView CreateTextureViewImpl(TextureViewDesc desc)
    {
        return new DXTextureView(this, desc);
    }

    protected override Sampler CreateSamplerImpl(SamplerDesc desc)
    {
        return new DXSampler(this, desc);
    }

    protected override Shader CreateShaderImpl(ShaderDesc desc)
    {
        return new DXShader(this, desc);
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

        SamplerHeap.Dispose();
        CbvSrvUavHeap.Dispose();
        DsvHeap.Dispose();
        RtvHeap.Dispose();

        CopyCommandQueue.Dispose();
        ComputeCommandQueue.Dispose();
        GraphicsCommandQueue.Dispose();

        DispatchMeshSignature.Dispose();
        DispatchSignature.Dispose();
        DrawIndexedSignature.Dispose();
        DrawSignature.Dispose();

        RootSignature.Dispose();

        InfoQueue1?.Dispose();

        Device10.Dispose();
        Adapter4.Dispose();
        Factory7.Dispose();

        D3D12.Dispose();
        DXGI.Dispose();
    }
}
