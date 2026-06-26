using Silk.NET.Core.Native;
using Silk.NET.Direct3D12;
using Silk.NET.DXGI;

namespace Zenith.NET.DirectX12;

internal unsafe class DXGraphicsContext(bool useValidationLayer) : GraphicsContext(GraphicsApi.DirectX12, useValidationLayer)
{
    public const ulong DefaultHeapAlignment = 4194304;

    public const uint Shader4ComponentMapping = 0x1688;

    public ComPtr<IDXGIFactory6> Factory;

    public ComPtr<IDXGIAdapter> Adapter;

    public ComPtr<ID3D12Device10> Device;

    public ComPtr<ID3D12RootSignature> RootSignature;

    public ComPtr<ID3D12CommandSignature> DrawSignature;

    public ComPtr<ID3D12CommandSignature> DrawIndexedSignature;

    public ComPtr<ID3D12CommandSignature> DispatchSignature;

    public ComPtr<ID3D12CommandSignature> DispatchMeshSignature;

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
        using ComPtr<ID3D12Debug> debug = new();
        if (useValidationLayer && D3D12.GetDebugInterface(SilkMarshal.GuidPtrOf<ID3D12Debug>(), (void**)debug.GetAddressOf()).IsSuccess())
        {
            debug.EnableDebugLayer();
        }

        DXGI.CreateDXGIFactory2(Convert.ToUInt32(useValidationLayer), SilkMarshal.GuidPtrOf<IDXGIFactory6>(), (void**)Factory.GetAddressOf()).Success();

        Factory.EnumAdapterByGpuPreference(0, GpuPreference.HighPerformance, SilkMarshal.GuidPtrOf<IDXGIAdapter>(), (void**)Adapter.GetAddressOf()).Success();

        if (!D3D12.CreateDevice((IUnknown*)Adapter.Handle, D3DFeatureLevel.Level120, SilkMarshal.GuidPtrOf<ID3D12Device10>(), (void**)Device.GetAddressOf()).IsSuccess())
        {
            throw new NotSupportedException("This device does not support DirectX 12.0 or higher.");
        }

        RootParameter1 rootParameter = new()
        {
            ParameterType = RootParameterType.TypeCbv,
            ShaderVisibility = ShaderVisibility.All,
            Descriptor = new() { Flags = RootDescriptorFlags.DataVolatile }
        };

        RootSignatureDesc2 rootSignatureDesc = new()
        {
            NumParameters = 1,
            PParameters = &rootParameter,
            Flags = RootSignatureFlags.AllowInputAssemblerInputLayout | RootSignatureFlags.CbvSrvUavHeapDirectlyIndexed | RootSignatureFlags.SamplerHeapDirectlyIndexed
        };

        VersionedRootSignatureDesc versionedRootSignatureDesc = new()
        {
            Version = D3DRootSignatureVersion.Version12,
            Desc12 = rootSignatureDesc
        };

        using ComPtr<ID3D10Blob> rootSignatureBlob = new();
        using ComPtr<ID3D10Blob> rootSignatureError = new();
        D3D12.SerializeVersionedRootSignature(&versionedRootSignatureDesc, rootSignatureBlob.GetAddressOf(), rootSignatureError.GetAddressOf()).Success();
        Device.CreateRootSignature(0, rootSignatureBlob.GetBufferPointer(), rootSignatureBlob.GetBufferSize(), SilkMarshal.GuidPtrOf<ID3D12RootSignature>(), (void**)RootSignature.GetAddressOf()).Success();

        IndirectArgumentDesc indirectArgumentDesc = new() { Type = IndirectArgumentType.Draw };

        CommandSignatureDesc commandSignatureDesc = new()
        {
            ByteStride = (uint)sizeof(IndirectDrawArgs),
            NumArgumentDescs = 1,
            PArgumentDescs = &indirectArgumentDesc
        };
        Device.CreateCommandSignature(&commandSignatureDesc, default(ID3D12RootSignature*), SilkMarshal.GuidPtrOf<ID3D12CommandSignature>(), (void**)DrawSignature.GetAddressOf()).Success();

        indirectArgumentDesc.Type = IndirectArgumentType.DrawIndexed;
        commandSignatureDesc.ByteStride = (uint)sizeof(IndirectDrawIndexedArgs);
        Device.CreateCommandSignature(&commandSignatureDesc, default(ID3D12RootSignature*), SilkMarshal.GuidPtrOf<ID3D12CommandSignature>(), (void**)DrawIndexedSignature.GetAddressOf()).Success();

        indirectArgumentDesc.Type = IndirectArgumentType.Dispatch;
        commandSignatureDesc.ByteStride = (uint)sizeof(IndirectDispatchArgs);
        Device.CreateCommandSignature(&commandSignatureDesc, default(ID3D12RootSignature*), SilkMarshal.GuidPtrOf<ID3D12CommandSignature>(), (void**)DispatchSignature.GetAddressOf()).Success();

        indirectArgumentDesc.Type = IndirectArgumentType.DispatchMesh;
        commandSignatureDesc.ByteStride = (uint)sizeof(IndirectDispatchMeshArgs);
        Device.CreateCommandSignature(&commandSignatureDesc, default(ID3D12RootSignature*), SilkMarshal.GuidPtrOf<ID3D12CommandSignature>(), (void**)DispatchMeshSignature.GetAddressOf()).Success();

        capabilities = new DXCapabilities(this);
        graphicsQueue = new DXCommandQueue(this, CommandQueueType.Graphics);
        computeQueue = new DXCommandQueue(this, CommandQueueType.Compute);
        copyQueue = new DXCommandQueue(this, CommandQueueType.Copy);
        validationLayer = useValidationLayer ? new DXValidationLayer(this) : null;
    }

    protected override SwapChain CreateSwapChainImpl(SwapChainDesc desc)
    {
        return new DXSwapChain(this, desc);
    }

    protected override Heap CreateHeapImpl(HeapDesc desc)
    {
        return new DXHeap(this, desc);
    }

    protected override SizeAndAlignment GetSizeAndAlignmentImpl(BufferDesc desc)
    {
        ResourceDesc1 resourceDesc = DXBuffer.ResourceDesc(desc);

        ResourceAllocationInfo info = Device.GetResourceAllocationInfo2(0, 1, &resourceDesc, default(ResourceAllocationInfo1*));

        return new(info.SizeInBytes, info.Alignment);
    }

    protected override SizeAndAlignment GetSizeAndAlignmentImpl(TextureDesc desc)
    {
        ResourceDesc1 resourceDesc = DXTexture.ResourceDesc(desc);

        ResourceAllocationInfo info = Device.GetResourceAllocationInfo2(0, 1, &resourceDesc, default(ResourceAllocationInfo1*));

        return new(info.SizeInBytes, info.Alignment);
    }

    protected override Buffer CreateBufferImpl(BufferDesc desc)
    {
        return new DXBuffer(this, desc);
    }

    protected override BufferView CreateBufferViewImpl(BufferViewDesc desc)
    {
        return new DXBufferView(this, desc);
    }

    protected override Texture CreateTextureImpl(TextureDesc desc)
    {
        return new DXTexture(this, desc);
    }

    protected override Texture CreateTextureImpl(TextureDesc desc, NativeTextureType nativeTextureType, nint nativeTexture)
    {
        ComPtr<ID3D12Resource> resource = new();
        Device.OpenSharedHandle((void*)nativeTexture, SilkMarshal.GuidPtrOf<ID3D12Resource>(), (void**)resource.GetAddressOf()).Success();

        return new DXTexture(this, desc, resource);
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
        return new DXGraphicsPipeline(this, desc);
    }

    protected override ComputePipeline CreateComputePipelineImpl(ComputePipelineDesc desc)
    {
        return new DXComputePipeline(this, desc);
    }

    protected override MeshShadingPipeline CreateMeshShadingPipelineImpl(MeshShadingPipelineDesc desc)
    {
        return new DXMeshShadingPipeline(this, desc);
    }

    protected override QueryHeap CreateQueryHeapImpl(QueryHeapDesc desc)
    {
        return new DXQueryHeap(this, desc);
    }

    protected override void Destroy()
    {
        base.Destroy();

        SamplerHeap.Dispose();
        CbvSrvUavHeap.Dispose();
        DsvHeap.Dispose();
        RtvHeap.Dispose();

        DispatchMeshSignature.Dispose();
        DispatchSignature.Dispose();
        DrawIndexedSignature.Dispose();
        DrawSignature.Dispose();

        RootSignature.Dispose();

        Device.Dispose();
        Adapter.Dispose();
        Factory.Dispose();

        D3D12.Dispose();
        DXGI.Dispose();
    }
}
