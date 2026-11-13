using Metal.NET;

namespace Zenith.NET.Metal;

internal class MTLHeap : Heap
{
    public MtlHeap Heap;

    public MTLHeap(MTLGraphicsContext context, HeapDesc desc) : base(context, desc)
    {
        using MTLHeapDescriptor descriptor = new()
        {
            Size = (nuint)desc.SizeInBytes,
            ResourceOptions = MTLFormats.Metal(desc.Residency),
            Type = MTLHeapType.Placement
        };

        Heap = context.Device.MakeHeap(descriptor);
    }

    public new MTLGraphicsContext Context => (MTLGraphicsContext)base.Context;

    public override nint GetNativeObject(NativeObjectType type)
    {
        return 0;
    }

    protected override Buffer CreateBufferImpl(ulong offsetInBytes, BufferDesc desc)
    {
        return new MTLBuffer(Context, desc, Heap.MakeBuffer(desc.SizeInBytes, MTLFormats.Metal(Desc.Residency), (nuint)offsetInBytes));
    }

    protected override Texture CreateTextureImpl(ulong offsetInBytes, TextureDesc desc)
    {
        using MTLTextureDescriptor descriptor = MTLTexture.Descriptor(desc);

        return new MTLTexture(Context, desc, Heap.MakeTexture(descriptor, (nuint)offsetInBytes));
    }

    protected override void SetResourceName(string name)
    {
        using NSString @string = new(name);

        Heap.Label = @string;
    }

    protected override void Destroy()
    {
        Heap.Dispose();
    }
}
