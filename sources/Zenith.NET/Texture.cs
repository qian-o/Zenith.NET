namespace Zenith.NET;

public abstract class Texture(GraphicsContext context, TextureDesc desc) : GraphicsResource(context)
{
    public TextureDesc Desc { get; } = desc;

    public abstract void Upload<T>(ReadOnlySpan<T> data, TexturePosition position, uint width, uint height, uint depth);
}
