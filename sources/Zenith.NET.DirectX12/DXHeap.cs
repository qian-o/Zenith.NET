using Silk.NET.Core.Native;
using Silk.NET.Direct3D12;
using Silk.NET.DXGI;

namespace Zenith.NET.DirectX12;

internal unsafe class DXHeap : Heap
{
    public ComPtr<ID3D12Heap> Heap;

    public DXHeap(DXGraphicsContext context, HeapDesc desc) : base(context, desc)
    {
        DxHeapDesc heapDesc = new()
        {
            SizeInBytes = ZenithHelper.Align(desc.SizeInBytes, DXGraphicsContext.DefaultHeapAlignment),
            Properties = new(DXFormats.DirectX12(desc.Residency)),
            Alignment = DXGraphicsContext.DefaultHeapAlignment
        };

        context.Device14.CreateHeap(&heapDesc, SilkMarshal.GuidPtrOf<ID3D12Heap>(), (void**)Heap.GetAddressOf()).Success();
    }

    public new DXGraphicsContext Context => (DXGraphicsContext)base.Context;

    public override nint GetNativeObject(NativeObjectType type)
    {
        return 0;
    }

    protected override Buffer CreateBufferImpl(ulong offsetInBytes, BufferDesc desc)
    {
        ResourceDesc1 resourceDesc = DXBuffer.ResourceDesc(desc);

        ComPtr<ID3D12Resource> resource = new();
        Context.Device14.CreatePlacedResource2(Heap,
                                               offsetInBytes,
                                               &resourceDesc,
                                               BarrierLayout.Undefined,
                                               default(ClearValue*),
                                               0,
                                               default(Format*),
                                               SilkMarshal.GuidPtrOf<ID3D12Resource>(),
                                               (void**)resource.GetAddressOf()).Success();

        return new DXBuffer(Context, desc, resource);
    }

    protected override Texture CreateTextureImpl(ulong offsetInBytes, TextureDesc desc)
    {
        ResourceDesc1 resourceDesc = DXTexture.ResourceDesc(desc);

        ComPtr<ID3D12Resource> resource = new();
        Context.Device14.CreatePlacedResource2(Heap,
                                               offsetInBytes,
                                               &resourceDesc,
                                               BarrierLayout.Undefined,
                                               default(ClearValue*),
                                               0,
                                               default(Format*),
                                               SilkMarshal.GuidPtrOf<ID3D12Resource>(),
                                               (void**)resource.GetAddressOf()).Success();

        return new DXTexture(Context, desc, resource);
    }

    protected override void SetResourceName(string name)
    {
        Heap.SetName(name).Success();
    }

    protected override void Destroy()
    {
        Heap.Dispose();
    }
}
