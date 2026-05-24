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
        ResourceDesc1 resourceDesc = new()
        {
            Dimension = ResourceDimension.Buffer,
            Alignment = 0,
            Width = ZenithHelper.Align(desc.SizeInBytes, 256u),
            Height = 1,
            DepthOrArraySize = 1,
            MipLevels = 1,
            Format = Format.FormatUnknown,
            SampleDesc = new(1, 0),
            Layout = TextureLayout.LayoutRowMajor,
            Flags = DXFormats.DirectX12(desc.Usages)
        };

        HeapProperties heapProperties = new(DXFormats.DirectX12(desc.Access));

        context.Device10.CreateCommittedResource3(&heapProperties,
                                                  HeapFlags.None,
                                                  &resourceDesc,
                                                  BarrierLayout.Undefined,
                                                  default,
                                                  default(ComPtr<ID3D12ProtectedResourceSession>),
                                                  0,
                                                  default,
                                                  out Resource).Success();

        GPUVirtualAddress = Resource.GetGPUVirtualAddress();

        View = new(context, new()
        {
            Buffer = this,
            OffsetInBytes = 0,
            SizeInBytes = desc.SizeInBytes,
            StrideInBytes = desc.StrideInBytes
        });
    }

    public DXBufferView View { get; }

    public override ResourceHandle UniformHandle => View.UniformHandle;

    public override ResourceHandle StorageReadOnlyHandle => View.StorageReadOnlyHandle;

    public override ResourceHandle StorageReadWriteHandle => View.StorageReadWriteHandle;

    public override nint GetNativeObject(NativeObjectType type)
    {
        return 0;
    }

    public override MappedMemory Map()
    {
        void* pointer;
        Resource.Map(0, default(ReadOnlySpan<DxRange>), &pointer).Success();

        return new((nint)pointer, Desc.SizeInBytes);
    }

    public override void Unmap()
    {
        Resource.Unmap(0, default(ReadOnlySpan<DxRange>));
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
}
