namespace Zenith.NET;

/// <summary>
/// Specifies options for geometry in acceleration structures.
/// </summary>
[Flags]
public enum GeometryFlags
{
    /// <summary>
    /// No flags are set.
    /// </summary>
    None = 0,

    /// <summary>
    /// Geometry is opaque.
    /// </summary>
    Opaque = 1 << 0,

    /// <summary>
    /// Geometry does not allow duplicate any-hit shader invocations.
    /// </summary>
    NoDuplicateAnyHitInvocation = 1 << 1
}
