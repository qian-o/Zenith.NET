namespace Zenith.NET;

/// <summary>
/// Describes a single vertex input element, including its format, semantic type, index, and byte offset.
/// </summary>
public record struct InputElementDesc : IDesc
{
    /// <summary>
    /// Special value indicating that the offset should be automatically aligned.
    /// </summary>
    public const int AppendAligned = -1;

    /// <summary>
    /// Initializes a new instance of the <see cref="InputElementDesc"/> struct with default values.
    /// </summary>
    public InputElementDesc()
    {
        Format = ElementFormat.UByte1;
        Type = ElementSemanticType.Position;
        Index = 0;
        OffsetInBytes = AppendAligned;
    }

    /// <summary>
    /// The data format of the input element.
    /// </summary>
    public ElementFormat Format { get; set; }

    /// <summary>
    /// The semantic type describing the intended use of the element (e.g., position, normal, color).
    /// </summary>
    public ElementSemanticType Type { get; set; }

    /// <summary>
    /// The semantic index, used to differentiate between multiple elements of the same type.
    /// </summary>
    public uint Index { get; set; }

    /// <summary>
    /// The byte offset of the element within the vertex. Use <see cref="AppendAligned"/> for automatic alignment.
    /// </summary>
    public int OffsetInBytes { get; set; }

    /// <summary>
    /// Validates the current <see cref="InputElementDesc"/> instance.
    /// </summary>
    /// <returns><c>true</c> if valid; otherwise, <c>false</c>.</returns>
    public readonly bool Validate()
    {
        if (!Enum.IsDefined(Format))
        {
            return false;
        }

        if (!Enum.IsDefined(Type))
        {
            return false;
        }

        return true;
    }
}
