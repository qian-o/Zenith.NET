namespace Zenith.NET;

/// <summary>
/// Describes a top-level acceleration structure (TLAS) for ray tracing, including the set of instances and build flags.
/// The TLAS references multiple bottom-level acceleration structures (BLAS) via <see cref="RayTracingInstanceDesc"/> instances,
/// enabling efficient ray traversal and instance-level transformations in a scene.
/// </summary>
public record struct TLASDesc : IDesc
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TLASDesc"/> struct with default values.
    /// </summary>
    public TLASDesc()
    {
        Instances = [];
        Flags = AccelerationStructureBuildFlags.None;
    }

    /// <summary>
    /// The array of instance descriptors, each referencing a BLAS and specifying instance-specific properties
    /// such as transformation, mask, and hit group contribution.
    /// </summary>
    public RayTracingInstanceDesc[] Instances { get; set; }

    /// <summary>
    /// Gets or sets the build flags that specify options for constructing or updating the acceleration structure.
    /// </summary>
    public AccelerationStructureBuildFlags Flags { get; set; }

    /// <summary>
    /// Validates the current <see cref="TLASDesc"/> instance.
    /// </summary>
    /// <returns><c>true</c> if valid; otherwise, <c>false</c>.</returns>
    public readonly bool Validate()
    {
        if (Instances is null || Instances.Length is 0)
        {
            return false;
        }

        if (Instances.Any(static item => !item.Validate()))
        {
            return false;
        }

        return true;
    }
}
