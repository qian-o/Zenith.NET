namespace Zenith.NET;

/// <summary>
/// Describes the layout of vertex input data, including its elements and stride.
/// </summary>
public record struct InputLayoutDesc : IDesc
{
    /// <summary>
    /// Initializes a new instance of the <see cref="InputLayoutDesc"/> struct with default values.
    /// </summary>
    public InputLayoutDesc()
    {
        Elements = [];
        StrideInBytes = 1;
    }

    /// <summary>
    /// The array of input element descriptors that define the vertex layout.
    /// </summary>
    public InputElementDesc[] Elements { get; set; }

    /// <summary>
    /// The total size, in bytes, of a single vertex.
    /// </summary>
    public uint StrideInBytes { get; set; }

    /// <summary>
    /// Validates the current <see cref="InputLayoutDesc"/> instance.
    /// </summary>
    /// <returns><c>true</c> if valid; otherwise, <c>false</c>.</returns>
    public readonly bool Validate()
    {
        if (Elements is null || Elements.Length is 0)
        {
            return false;
        }

        if (Elements.Any(static item => !item.Validate()))
        {
            return false;
        }

        if (StrideInBytes is 0)
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Adds a new input element to the layout.
    /// If the element's offset is set to <see cref="InputElementDesc.AppendAligned"/>, it will be automatically aligned to the current stride.
    /// The stride is incremented by the size of the new element's format.
    /// </summary>
    /// <param name="element">The input element descriptor to add.</param>
    /// <returns>The updated <see cref="InputLayoutDesc"/> instance.</returns>
    public InputLayoutDesc Add(InputElementDesc element)
    {
        if (element.OffsetInBytes is InputElementDesc.AppendAligned)
        {
            element.OffsetInBytes = (int)StrideInBytes;
        }

        Elements = [.. Elements, element];

        StrideInBytes += GetFormatSizeInBytes(element.Format);

        return this;
    }

    /// <summary>
    /// Gets the size, in bytes, of the specified element format.
    /// </summary>
    /// <param name="format">The element format.</param>
    /// <returns>The size in bytes.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if the format is unsupported.</exception>
    private static uint GetFormatSizeInBytes(ElementFormat format)
    {
        return format switch
        {
            ElementFormat.UByte1 or
            ElementFormat.Byte1 or
            ElementFormat.UByte1Normalized or
            ElementFormat.Byte1Normalized => 1,

            ElementFormat.UByte2 or
            ElementFormat.Byte2 or
            ElementFormat.UByte2Normalized or
            ElementFormat.Byte2Normalized or
            ElementFormat.UShort1 or
            ElementFormat.Short1 or
            ElementFormat.UShort1Normalized or
            ElementFormat.Short1Normalized or
            ElementFormat.Half1 => 2,

            ElementFormat.UByte4 or
            ElementFormat.Byte4 or
            ElementFormat.UByte4Normalized or
            ElementFormat.Byte4Normalized or
            ElementFormat.UShort2 or
            ElementFormat.Short2 or
            ElementFormat.UShort2Normalized or
            ElementFormat.Short2Normalized or
            ElementFormat.Half2 or
            ElementFormat.Float1 or
            ElementFormat.UInt1 or
            ElementFormat.Int1 => 4,

            ElementFormat.UShort4 or
            ElementFormat.Short4 or
            ElementFormat.UShort4Normalized or
            ElementFormat.Short4Normalized or
            ElementFormat.Half4 or
            ElementFormat.Float2 or
            ElementFormat.UInt2 or
            ElementFormat.Int2 => 8,

            ElementFormat.Float3 or
            ElementFormat.UInt3 or
            ElementFormat.Int3 => 12,

            ElementFormat.Float4 or
            ElementFormat.UInt4 or
            ElementFormat.Int4 => 16,

            _ => throw new ArgumentOutOfRangeException(nameof(format), $"Unsupported format: {format}")
        };
    }
}
