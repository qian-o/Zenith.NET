namespace Zenith.NET;

[Flags]
public enum ClearOptions
{
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
