namespace Zenith.NET;

/// <summary>
/// Specifies which buffers to clear during a clear operation.
/// </summary>
[Flags]
public enum ClearFlags
{
    /// <summary>
    /// No flags are set.
    /// </summary>
    None = 0,

    /// <summary>
    /// Clear the color buffer.
    /// </summary>
    Color = 1 << 0,

    /// <summary>
    /// Clear the depth buffer.
    /// </summary>
    Depth = 1 << 1,

    /// <summary>
    /// Clear the stencil buffer.
    /// </summary>
    Stencil = 1 << 2,

    /// <summary>
    /// Clear all buffers.
    /// </summary>
    All = Color | Depth | Stencil
}
