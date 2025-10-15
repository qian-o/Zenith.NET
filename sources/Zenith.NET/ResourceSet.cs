namespace Zenith.NET;

public abstract class ResourceSet(GraphicsContext context, ResourceSetDesc desc) : GraphicsResource(context)
{
    private ResourceSetDesc desc = desc;

    public ref readonly ResourceSetDesc Desc => ref desc;
}
