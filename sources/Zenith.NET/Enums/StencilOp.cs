namespace Zenith.NET;

/// <summary>
/// Specifies the operation to perform on stencil buffer values during stencil testing.
/// </summary>
public enum StencilOp
{
    /// <summary>
    /// Keep the current value.
    /// </summary>
    Keep,

    /// <summary>
    /// Set the value to zero.
    /// </summary>
    Zero,

    /// <summary>
    /// Replace the value with the reference value.
    /// </summary>
    Replace,

    /// <summary>
    /// Increment the value and clamp.
    /// </summary>
    IncrementAndClamp,

    /// <summary>
    /// Decrement the value and clamp.
    /// </summary>
    DecrementAndClamp,

    /// <summary>
    /// Invert the current value.
    /// </summary>
    Invert,

    /// <summary>
    /// Increment the value and wrap.
    /// </summary>
    IncrementAndWrap,

    /// <summary>
    /// Decrement the value and wrap.
    /// </summary>
    DecrementAndWrap
}
