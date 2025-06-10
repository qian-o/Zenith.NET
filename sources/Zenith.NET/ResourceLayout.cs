namespace Zenith.NET;

public abstract class ResourceLayout(GraphicsContext context, ResourceLayoutDesc desc) : GraphicsResource(context)
{
    public ResourceLayoutDesc Desc { get; } = desc;
}