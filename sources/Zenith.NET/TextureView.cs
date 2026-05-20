namespace Zenith.NET;

public abstract class TextureView(GraphicsContext context, TextureViewDesc desc) : GraphicsResource(context)
{
    private TextureViewDesc desc = desc;

    public ref readonly TextureViewDesc Desc => ref desc;

    public ResourceHandle SampledHandle { get; }

    public ResourceHandle StorageHandle { get; }
}
