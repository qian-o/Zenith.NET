namespace Zenith.NET;

/// <summary>
/// Represents a top-level acceleration structure (TLAS) resource, which encapsulates a collection of instance descriptors
/// referencing bottom-level acceleration structures (BLAS) for use in ray tracing and GPU acceleration structure operations.
/// </summary>
public abstract class TLAS(GraphicsContext context, TLASDesc desc) : GraphicsResource(context)
{
    /// <summary>
    /// Gets the TLAS description.
    /// </summary>
    public TLASDesc Desc { get; } = desc;
}
