namespace Zenith.NET;

public abstract class ResourceLayout(GraphicsContext context, ResourceLayoutDesc desc) : GraphicsResource(context)
{
    private ResourceLayoutDesc desc = desc;

    public ref readonly ResourceLayoutDesc Desc => ref desc;
}