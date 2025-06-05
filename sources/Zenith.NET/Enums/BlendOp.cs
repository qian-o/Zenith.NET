namespace Zenith.NET;

/// <summary>
/// Specifies the operation used to combine source and destination colors in blending.
/// </summary>
public enum BlendOp
{
    /// <summary>
    /// Source and destination are added.
    /// </summary>
    Add,

    /// <summary>
    /// Destination is subtracted from source.
    /// </summary>
    Subtract,

    /// <summary>
    /// Source is subtracted from destination.
    /// </summary>
    ReverseSubtract,

    /// <summary>
    /// The minimum of source and destination is selected.
    /// </summary>
    Min,

    /// <summary>
    /// The maximum of source and destination is selected.
    /// </summary>
    Max
}
