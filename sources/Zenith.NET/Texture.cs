namespace Zenith.NET;

public abstract class Texture(GraphicsContext context, TextureDesc desc) : GraphicsResource(context)
{
    private TextureDesc desc = desc;

    public ref readonly TextureDesc Desc => ref desc;

    public abstract void Upload<T>(ReadOnlySpan<T> data, TexturePosition position, uint width, uint height, uint depth);
}
