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
            Flags = desc.Flags.HasFlag(BufferUsageFlags.UnorderedAccess) ? ResourceFlags.AllowUnorderedAccess : ResourceFlags.None
        };

        HeapProperties heapProperties = new(HeapType.Default);
        ResourceStates initialState = ResourceStates.Common;

        if (desc.Flags.HasFlag(BufferUsageFlags.Dynamic))
        {
            heapProperties = new HeapProperties(HeapType.Upload);
            initialState = ResourceStates.GenericRead;
        }

        Context.Device.CreateCommittedResource(&heapProperties,
                                               HeapFlags.None,
                                               &resourceDesc,
                                               initialState,
                                               null,
                                               out Resource).Success();

        View = new(context, new()
        {
            Buffer = this,
            OffsetInBytes = 0,
            SizeInBytes = desc.SizeInBytes,
            StrideInBytes = desc.StrideInBytes
        });

        States = initialState;
    }

    public new DXGraphicsContext Context => (DXGraphicsContext)base.Context;

    public DXBufferView View { get; }

    public ResourceStates States { get; set; }

    public override MappedMemory Map()
    {
        void* pointer;
        Resource.Map(0, (Range*)null, &pointer).Success();

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
        Resource.Unmap(0, (Range*)null);
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
