namespace Zenith.NET;

/// <summary>
/// Describes a bottom-level acceleration structure (BLAS), which consists of a collection of geometry descriptors.
/// Used for ray tracing and other GPU acceleration structure operations.
/// </summary>
public record struct BLASDesc : IDesc
{
    /// <summary>
    /// Initializes a new instance of the <see cref="BLASDesc"/> struct with default values.
    /// </summary>
    public BLASDesc()
    {
        Geometries = [];
    }

    /// <summary>
    /// Gets or sets the array of geometry descriptors that define the contents of the BLAS.
    /// Each geometry must implement <see cref="IGeometryDesc"/> and be valid.
    /// </summary>
    public IGeometryDesc[] Geometries { get; set; }

    /// <summary>
    /// Validates the current <see cref="BLASDesc"/> instance.
    /// </summary>
    /// <returns><c>true</c> if valid; otherwise, <c>false</c>.</returns>
    public readonly bool Validate()
    {
        if (Geometries is null || Geometries.Length is 0)
        {
            return false;
        }

        if (Geometries.Any(static item => !item.Validate()))
        {
            return false;
        }

        return true;
    }
}
