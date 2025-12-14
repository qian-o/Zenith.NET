using Silk.NET.Core.Native;
using Silk.NET.Direct3D12;

namespace Zenith.NET.DirectX12;

internal unsafe class DXBuffer : Buffer
{
    public ComPtr<ID3D12Resource> Resource;

    public DXBuffer(GraphicsContext context, BufferDesc desc) : base(context, desc)
    {
        ResourceDesc resourceDesc = new()
        {
            Dimension = ResourceDimension.Buffer,
            Width = ZenithHelper.Align(desc.SizeInBytes, 256u),
            Height = 1,
            DepthOrArraySize = 1,
            MipLevels = 1,
            SampleDesc = new(1, 0),
            Layout = TextureLayout.LayoutRowMajor,
            Flags = DXFormats.DirectX12(desc.Flags).Flags
        };

        HeapProperties heapProperties = new(desc.Flags.HasFlag(BufferUsageFlags.Dynamic) ? HeapType.Upload : HeapType.Default);

        Context.Device.CreateCommittedResource(&heapProperties,
                                               HeapFlags.None,
                                               &resourceDesc,
                                               States = DXFormats.DirectX12(desc.Flags).States,
                                               null,
                                               out Resource).Success();

        View = new(context, new()
        {
            Buffer = this,
            OffsetInBytes = 0,
            SizeInBytes = desc.SizeInBytes,
            StrideInBytes = desc.StrideInBytes
        });
    }

    public new DXGraphicsContext Context => (DXGraphicsContext)base.Context;

    public DXBufferView View { get; }

    public ResourceStates States { get; set; }

    public override MappedMemory Map()
    {
        void* pointer;
        Resource.Map(0, (DxRange*)null, &pointer).Success();

        return new()
        {
            Pointer = (nint)pointer,
            SizeInBytes = Desc.SizeInBytes,
            RowPitch = Desc.SizeInBytes,
            SlicePitch = Desc.SizeInBytes
        };
    }

    public override void Unmap()
    {
        Resource.Unmap(0, (DxRange*)null);
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
