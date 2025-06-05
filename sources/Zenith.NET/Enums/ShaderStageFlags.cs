namespace Zenith.NET;

/// <summary>
/// Specifies shader stages for pipeline configuration and resource binding.
/// Supports both traditional graphics and ray tracing shader stages.
/// </summary>
[Flags]
public enum ShaderStageFlags
{
    None = 0,

    /// <summary>
    /// The vertex shader stage.
    /// </summary>
    Vertex = 1 << 0,

    /// <summary>
    /// The hull (tessellation control) shader stage.
    /// </summary>
    Hull = 1 << 1,

    /// <summary>
    /// The domain (tessellation evaluation) shader stage.
    /// </summary>
    Domain = 1 << 2,

    /// <summary>
    /// The geometry shader stage.
    /// </summary>
    Geometry = 1 << 3,

    /// <summary>
    /// The pixel (fragment) shader stage.
    /// </summary>
    Pixel = 1 << 4,

    /// <summary>
    /// The compute shader stage.
    /// </summary>
    Compute = 1 << 5,

    /// <summary>
    /// The ray generation shader stage (ray tracing).
    /// </summary>
    RayGeneration = 1 << 6,

    /// <summary>
    /// The miss shader stage (ray tracing).
    /// </summary>
    Miss = 1 << 7,

    /// <summary>
    /// The closest hit shader stage (ray tracing).
    /// </summary>
    ClosestHit = 1 << 8,

    /// <summary>
    /// The any hit shader stage (ray tracing).
    /// </summary>
    AnyHit = 1 << 9,

    /// <summary>
    /// The intersection shader stage (ray tracing).
    /// </summary>
    Intersection = 1 << 10,

    /// <summary>
    /// The callable shader stage (ray tracing).
    /// </summary>
    Callable = 1 << 11
}