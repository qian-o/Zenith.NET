namespace Zenith.NET;

/// <summary>
/// Describes the feature capabilities of a graphics device or context.
/// </summary>
public abstract class Capabilities
{
    /// <summary>
    /// Gets a value indicating whether ray query is supported.
    /// </summary>
    public abstract bool IsRayQuerySupported { get; }

    /// <summary>
    /// Gets a value indicating whether ray tracing is supported.
    /// </summary>
    public abstract bool IsRayTracingSupported { get; }
}
