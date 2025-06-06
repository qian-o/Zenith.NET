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
}
