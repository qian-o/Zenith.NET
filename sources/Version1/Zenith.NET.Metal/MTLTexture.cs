namespace Zenith.NET.Metal;

internal class MTLTexture : Texture
{
    public MtlTexture Texture;

    public MTLTexture(MTLGraphicsContext context, TextureDesc desc) : base(context, desc)
    {
        Heap = new(context, desc, out Texture);
    }

    public MTLTexture(MTLGraphicsContext context, TextureDesc desc, MtlTexture texture) : base(context, desc)
    {
        Texture = texture;
    }

    public MTLHeap? Heap { get; }

    protected override void SetResourceName(string name)
    {
        Texture.Label = name;
    }

    protected override void Destroy()
    {
        if (Heap is not null)
        {
            Texture.Dispose();

            Heap.Dispose();
        }
    }
}
