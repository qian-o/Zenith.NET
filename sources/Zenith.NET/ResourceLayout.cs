namespace Zenith.NET;

/// <summary>
/// Represents a GPU resource layout, describing the organization of resources bound to a pipeline.
/// </summary>
public abstract class ResourceLayout(GraphicsContext context, ResourceLayoutDesc desc) : GraphicsResource(context)
{
    /// <summary>
    /// Gets the resource layout description.
    /// </summary>
    public ResourceLayoutDesc Desc { get; } = desc;
}