using Silk.NET.Core.Native;
using Silk.NET.Direct3D12;
using Silk.NET.DXGI;

namespace Zenith.NET.DirectX12;

internal unsafe class DXBuffer : Buffer
{
    public ComPtr<ID3D12Resource> Resource;

    public ulong GPUVirtualAddress;

    public DXBuffer(DXGraphicsContext context, BufferDesc desc) : base(context, desc)
    {
        ResourceDesc1 resourceDesc = ResourceDesc(desc);

        HeapProperties heapProperties = new() { Type = DXFormats.DirectX12(desc.Residency) };

        context.Device.CreateCommittedResource3(&heapProperties,
                                                HeapFlags.None,
                                                &resourceDesc,
                                                BarrierLayout.Undefined,
                                                default(ClearValue*),
                                                default(ID3D12ProtectedResourceSession*),
                                                0,
                                                default(Format*),
                                                SilkMarshal.GuidPtrOf<ID3D12Resource>(),
                                                (void**)Resource.GetAddressOf()).Success();

        GPUVirtualAddress = Resource.GetGPUVirtualAddress();

        View = new(context, new()
        {
            Buffer = this,
            SizeInBytes = desc.SizeInBytes,
            StrideInBytes = desc.StrideInBytes
        });
    }

    public DXBuffer(DXGraphicsContext context, BufferDesc desc, ComPtr<ID3D12Resource> resource) : base(context, desc)
    {
        Resource = resource;

        GPUVirtualAddress = Resource.GetGPUVirtualAddress();

        View = new(context, new()
        {
            Buffer = this,
            SizeInBytes = desc.SizeInBytes,
            StrideInBytes = desc.StrideInBytes
        });
    }

    public DXBuffer(DXGraphicsContext context, BufferDesc desc, ResourceFlags flags) : base(context, desc)
    {
        ResourceDesc1 resourceDesc = ResourceDesc(desc);
        resourceDesc.Flags |= flags;

        HeapProperties heapProperties = new() { Type = DXFormats.DirectX12(desc.Residency) };

        context.Device.CreateCommittedResource3(&heapProperties,
                                                HeapFlags.None,
                                                &resourceDesc,
                                                BarrierLayout.Undefined,
                                                default(ClearValue*),
                                                default(ID3D12ProtectedResourceSession*),
                                                0,
                                                default(Format*),
                                                SilkMarshal.GuidPtrOf<ID3D12Resource>(),
                                                (void**)Resource.GetAddressOf()).Success();

        GPUVirtualAddress = Resource.GetGPUVirtualAddress();

        View = new(context, new()
        {
            Buffer = this,
            SizeInBytes = desc.SizeInBytes,
            StrideInBytes = desc.StrideInBytes
        });
    }

    public DXBufferView View { get; }

    public override ResourceHandle ConstantHandle => View.ConstantHandle;

    public override ResourceHandle StorageReadOnlyHandle => View.StorageReadOnlyHandle;

    public override ResourceHandle StorageReadWriteHandle => View.StorageReadWriteHandle;

    public override nint GetNativeObject(NativeObjectType type)
    {
        return type switch
        {
            NativeObjectType.D3D12GpuVirtualAddress => (nint)GPUVirtualAddress,
            NativeObjectType.D3D12Resource => (nint)Resource.Handle,
            _ => default
        };
    }

    public override nint Map()
    {
        void* pointer;
        Resource.Map(0, default(DxRange*), &pointer).Success();

        return (nint)pointer;
    }

    public override void Unmap()
    {
        Resource.Unmap(0, default(DxRange*));
    }

    protected override void SetResourceName(string name)
    {
        Resource.SetName(name).Success();
    }

    protected override void Destroy()
    {
        View.Dispose();
        Resource.Dispose();
    }

    public static ResourceDesc1 ResourceDesc(BufferDesc desc)
    {
        return new()
        {
            Dimension = ResourceDimension.Buffer,
            Width = ZenithHelper.Align(desc.SizeInBytes, 256u),
            Height = 1,
            DepthOrArraySize = 1,
            MipLevels = 1,
            SampleDesc = new() { Count = 1 },
            Layout = DxTextureLayout.LayoutRowMajor,
            Flags = DXFormats.DirectX12(desc.Usages)
        };
    }
}
