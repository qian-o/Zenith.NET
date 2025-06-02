namespace Zenith.NET;

public abstract class Texture(GraphicsContext context, TextureDesc desc) : GraphicsResource(context)
{
    public TextureDesc Desc { get; } = desc;
}
