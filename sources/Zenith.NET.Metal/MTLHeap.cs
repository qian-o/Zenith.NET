namespace Zenith.NET.Metal;

internal class MTLHeap : GraphicsResource
{
    public MtlHeap Heap;

    public MTLHeap(MTLGraphicsContext context, BufferDesc desc, out MtlBuffer buffer) : base(context)
    {
        throw new NotImplementedException();
    }

    public MTLHeap(MTLGraphicsContext context, TextureDesc desc, out MtlTexture texture) : base(context)
    {
        throw new NotImplementedException();
    }

    public new MTLGraphicsContext Context => (MTLGraphicsContext)base.Context;

    protected override void SetResourceName(string name)
    {
    }

    protected override void Destroy()
    {
        Context.RemoveAllocation(Heap);

        Heap.Dispose();
    }
}
