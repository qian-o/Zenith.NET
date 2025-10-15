namespace Zenith.NET;

public abstract class TextureView(GraphicsContext context, TextureViewDesc desc) : GraphicsResource(context), IBindableResource
{
    private TextureViewDesc desc = desc;

    public ref readonly TextureViewDesc Desc => ref desc;
}
