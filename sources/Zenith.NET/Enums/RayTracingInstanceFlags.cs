namespace Zenith.NET;

/// <summary>
/// Specifies options for instances in top-level acceleration structures.
/// </summary>
[Flags]
public enum RayTracingInstanceFlags
{
    /// <summary>
    /// No flags are set.
    /// </summary>
    None = 0,

    /// <summary>
    /// Disables front/back face culling for this instance.
    /// </summary>
    TriangleCullDisable = 1 << 0,

    /// <summary>
    /// This flag reverses front and back facings.
    /// </summary>
    TriangleFrontCounterClockwise = 1 << 1,

    /// <summary>
    /// Applied to all the geometries in the bottom-level acceleration structure referenced by the instance.
    /// </summary>
    ForceOpaque = 1 << 2,

    /// <summary>
    /// Applied to any of the geometries in the bottom-level acceleration structure referenced by the instance.
    /// </summary>
    ForceNoOpaque = 1 << 3
}
