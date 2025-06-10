namespace Zenith.NET;

public abstract class ResourceSet(GraphicsContext context, ResourceSetDesc desc) : GraphicsResource(context)
{
    public ResourceSetDesc Desc { get; } = desc;
}
