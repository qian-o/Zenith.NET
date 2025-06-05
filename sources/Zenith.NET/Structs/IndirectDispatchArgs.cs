namespace Zenith.NET;

/// <summary>
/// Describes the arguments required for an indirect compute dispatch call.
/// </summary>
public record struct IndirectDispatchArgs
{
    /// <summary>
    /// Initializes a new instance of the <see cref="IndirectDispatchArgs"/> struct with default values.
    /// </summary>
    public IndirectDispatchArgs()
    {
        GroupCountX = 1;
        GroupCountY = 1;
        GroupCountZ = 1;
    }

    /// <summary>
    /// The number of thread groups dispatched in the X dimension.
    /// </summary>
    public uint GroupCountX { get; set; }

    /// <summary>
    /// The number of thread groups dispatched in the Y dimension.
    /// </summary>
    public uint GroupCountY { get; set; }

    /// <summary>
    /// The number of thread groups dispatched in the Z dimension.
    /// </summary>
    public uint GroupCountZ { get; set; }
}
