namespace Zenith.NET;

/// <summary>
/// Specifies which faces of a primitive are culled during rasterization.
/// </summary>
public enum CullMode
{
    /// <summary>
    /// No culling is performed.
    /// </summary>
    None,

    /// <summary>
    /// Cull front-facing primitives.
    /// </summary>
    Front,

    /// <summary>
    /// Cull back-facing primitives.
    /// </summary>
    Back
}
