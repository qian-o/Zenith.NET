namespace Zenith.NET;

public abstract class Texture(GraphicsContext context, TextureDesc desc) : GraphicsResource(context), IBindableResource
{
    private TextureDesc desc = desc;

    public ref readonly TextureDesc Desc => ref desc;

    public abstract TextureView View { get; }

    public abstract void Upload<T>(ReadOnlySpan<T> data, TextureSlice slice, TextureOffset offset, TextureExtent extent);

    public abstract void Download<T>(Span<T> data, TextureSlice slice, TextureOffset offset, TextureExtent extent);
}
