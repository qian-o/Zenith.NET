namespace Zenith.NET;

/// <summary>
/// Describes a set of axis-aligned bounding boxes (AABBs) for use in acceleration structures.
/// </summary>
public record struct AABBsDesc : IGeometryDesc
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AABBsDesc"/> struct with default values.
    /// </summary>
    public AABBsDesc()
    {
        Buffer = null!;
        Count = 0;
        StrideInBytes = 0;
        OffsetInBytes = 0;
        Flags = GeometryFlags.None;
    }

    /// <summary>
    /// The buffer containing the AABB data.
    /// </summary>
    public Buffer Buffer { get; set; }

    /// <summary>
    /// The number of AABBs described by this structure.
    /// </summary>
    public uint Count { get; set; }

    /// <summary>
    /// The stride, in bytes, between consecutive AABBs in the buffer.
    /// </summary>
    public uint StrideInBytes { get; set; }

    /// <summary>
    /// The offset, in bytes, from the start of the buffer to the first AABB.
    /// </summary>
    public uint OffsetInBytes { get; set; }

    /// <summary>
    /// Gets or sets the geometry flags that specify options for the AABBs in acceleration structures.
    /// </summary>
    public GeometryFlags Flags { get; set; }

    /// <summary>
    /// Validates the current <see cref="AABBsDesc"/> instance.
    /// </summary>
    /// <returns><c>true</c> if valid; otherwise, <c>false</c>.</returns>
    public readonly bool Validate()
    {
        if (Buffer is null)
        {
            return false;
        }

        if (Count is 0)
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
