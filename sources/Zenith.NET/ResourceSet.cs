namespace Zenith.NET;

/// <summary>
/// Represents a GPU resource set, encapsulating a group of resources to be bound together according to a specified layout.
/// </summary>
public abstract class ResourceSet(GraphicsContext context, ResourceSetDesc desc) : GraphicsResource(context)
{
    /// <summary>
    /// Gets the resource set description.
    /// </summary>
    public ResourceSetDesc Desc { get; } = desc;
}
