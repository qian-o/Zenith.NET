namespace Zenith.NET;

public enum StencilOperation
{
    /// <summary>
    /// Keep the existing value.
    /// </summary>
    Keep,

    /// <summary>
    /// Sets the value to 0.
    /// </summary>
    Zero,

    /// <summary>
    /// Replaces the existing value with the reference value.
    /// </summary>
    Replace,

    /// <summary>
    /// Increments the existing value and clamps it to the maximum representable unsigned value.
    /// </summary>
    IncrementAndClamp,

    /// <summary>
    /// Decrements the existing value and clamps it to 0.
    /// </summary>
    DecrementAndClamp,

    /// <summary>
    /// Bitwise-inverts the existing value.
    /// </summary>
    Invert,

    /// <summary>
    /// Increments the existing value and wraps it to 0 when it exceeds the maximum representable unsigned value.
    /// </summary>
    IncrementAndWrap,

    /// <summary>
    /// Decrements the existing value and wraps it to the maximum representable unsigned value if it would be reduced below 0.
    /// </summary>
    DecrementAndWrap
}
