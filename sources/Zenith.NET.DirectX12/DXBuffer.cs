using Silk.NET.Core.Native;
using Silk.NET.Direct3D12;

namespace Zenith.NET.DirectX12;

internal unsafe class DXBuffer : Buffer
{
    public ComPtr<ID3D12Resource> Resource;

    public ulong GPUVirtualAddress;

    public DXBuffer(DXGraphicsContext context, BufferDesc desc) : base(context, desc)
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

        Heap = new(context, resourceDesc, DXFormats.DirectX12(desc.Flags).Type, HeapFlags.AllowOnlyBuffers);

        context.Device.CreatePlacedResource(Heap.Heap, 0, &resourceDesc, DXFormats.DirectX12(desc.Flags).States, null, out Resource).Success();

        GPUVirtualAddress = Resource.GetGPUVirtualAddress();

        View = new(context, new()
        {
            Buffer = this,
            OffsetInBytes = 0,
            SizeInBytes = desc.SizeInBytes,
            StrideInBytes = desc.StrideInBytes
        });
    }

    public DXHeap Heap { get; }

    public DXBufferView View { get; }

    protected override MappedMemory MapImpl()
    {
        void* pointer;
        Resource.Map(0, (DxRange*)null, &pointer).Success();

        return new() { Pointer = (nint)pointer, SizeInBytes = Desc.SizeInBytes };
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

        Heap.Dispose();
    }
}
