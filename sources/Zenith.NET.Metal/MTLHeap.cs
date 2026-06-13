using Metal.NET;

namespace Zenith.NET.Metal;

internal class MTLHeap : Heap
{
    public MtlHeap Heap;

    public MTLHeap(MTLGraphicsContext context, HeapDesc desc) : base(context, desc)
    {
        context.Register(Heap = context.Device.MakeHeap(new()
        {
            Size = (nuint)desc.SizeInBytes,
            ResourceOptions = MTLFormats.Metal(desc.Residency),
            Type = MTLHeapType.Placement
        }));
    }

    public new MTLGraphicsContext Context => (MTLGraphicsContext)base.Context;

    public override nint GetNativeObject(NativeObjectType type)
    {
        return 0;
    }

    protected override Buffer CreateBufferImpl(ulong offsetInBytes, BufferDesc desc)
    {
        return new MTLBuffer(Context, desc, Heap.MakeBuffer(desc.SizeInBytes, MTLFormats.Metal(desc.Residency), (nuint)offsetInBytes));
    }

    protected override Texture CreateTextureImpl(ulong offsetInBytes, TextureDesc desc)
    {
        return new MTLTexture(Context, desc, Heap.MakeTexture(MTLTexture.Descriptor(desc), (nuint)offsetInBytes));
    }

    protected override void SetResourceName(string name)
    {
        Heap.Label = name;
    }

    protected override void Destroy()
    {
        Context.Unregister(Heap);

        Heap.Dispose();
    }
}
