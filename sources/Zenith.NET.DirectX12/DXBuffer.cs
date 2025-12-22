using Silk.NET.Core.Native;
using Silk.NET.Direct3D12;

namespace Zenith.NET.DirectX12;

internal unsafe class DXBuffer : Buffer
{
    public ComPtr<ID3D12Resource> Resource;

    public DXBuffer(DXGraphicsContext context, BufferDesc desc) : base(context, desc)
    {
        Heap = new(context, this, out ResourceDesc resourceDesc);

        context.Device.CreatePlacedResource(Heap.Heap, 0, &resourceDesc, States = DXFormats.DirectX12(desc.Flags).States, null, out Resource).Success();

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

    public void TransitionStates(DXCommandBuffer commandBuffer, ResourceStates newStates)
    {
        if (States == newStates)
        {
            return;
        }

        ResourceBarrier barrier = new()
        {
            Type = ResourceBarrierType.Transition,
            Transition = new()
            {
                PResource = Resource,
                Subresource = 0,
                StateBefore = States,
                StateAfter = newStates
            }
        };

        commandBuffer.GraphicsCommandList.ResourceBarrier(1, &barrier);

        States = newStates;
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
