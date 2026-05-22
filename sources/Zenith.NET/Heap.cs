namespace Zenith.NET;

public abstract class Heap(GraphicsContext context, HeapDesc desc) : GraphicsResource(context)
{
    private HeapDesc desc = desc;

    public ref readonly HeapDesc Desc => ref desc;

    public Buffer CreateBuffer(ulong offsetInBytes, BufferDesc desc)
    {
        Context.ValidationLayer?.ValidateDesc(desc);

        return CreateBufferImpl(offsetInBytes, desc);
    }

    public Texture CreateTexture(ulong offsetInBytes, TextureDesc desc)
    {
        Context.ValidationLayer?.ValidateDesc(desc);

        return CreateTextureImpl(offsetInBytes, desc);
    }

    protected abstract Buffer CreateBufferImpl(ulong offsetInBytes, BufferDesc desc);

    protected abstract Texture CreateTextureImpl(ulong offsetInBytes, TextureDesc desc);
}
