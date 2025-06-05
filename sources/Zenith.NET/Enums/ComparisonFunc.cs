namespace Zenith.NET;

/// <summary>
/// Specifies a function used to compare source and destination data, typically for depth or stencil testing.
/// </summary>
public enum ComparisonFunc
{
    /// <summary>
    /// Never passes the comparison.
    /// </summary>
    Never,

    /// <summary>
    /// Passes if the source data is less than the destination data.
    /// </summary>
    Less,

    /// <summary>
    /// Passes if the source data is equal to the destination data.
    /// </summary>
    Equal,

    /// <summary>
    /// Passes if the source data is less than or equal to the destination data.
    /// </summary>
    LessEqual,

    /// <summary>
    /// Passes if the source data is greater than the destination data.
    /// </summary>
    Greater,

    /// <summary>
    /// Passes if the source data is not equal to the destination data.
    /// </summary>
    NotEqual,

    /// <summary>
    /// Passes if the source data is greater than or equal to the destination data.
    /// </summary>
    GreaterEqual,

    /// <summary>
    /// Always passes the comparison.
    /// </summary>
    Always
}
