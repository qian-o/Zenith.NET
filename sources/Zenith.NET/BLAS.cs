namespace Zenith.NET;

/// <summary>
/// Represents a bottom-level acceleration structure (BLAS) resource, which encapsulates a collection of geometry descriptors
/// for use in ray tracing and GPU acceleration structure operations.
/// </summary>
public abstract class BLAS(GraphicsContext context, BLASDesc desc) : GraphicsResource(context)
{
    /// <summary>
    /// Gets the BLAS description.
    /// </summary>
    public BLASDesc Desc { get; } = desc;
}
