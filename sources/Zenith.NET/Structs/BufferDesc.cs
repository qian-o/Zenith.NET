namespace Zenith.NET;

/// <summary>
/// Describes a buffer resource, including its size, structure stride, and usage flags.
/// </summary>
public record struct BufferDesc : IDesc
{
    /// <summary>
    /// Initializes a new instance of the <see cref="BufferDesc"/> struct with default values.
    /// </summary>
    public BufferDesc()
    {
        SizeInBytes = 0;
        StructureStrideInBytes = 1;
        Flags = BufferUsageFlags.None;
    }

    /// <summary>
    /// The desired capacity of the buffer, in bytes.
    /// </summary>
    public uint SizeInBytes { get; set; }

    /// <summary>
    /// The byte stride of the structure. Set to 1 if the buffer is not structured.
    /// </summary>
    public uint StructureStrideInBytes { get; set; }

    /// <summary>
    /// Indicates the intended usage of the buffer.
    /// </summary>
    public BufferUsageFlags Flags { get; set; }

    /// <summary>
    /// Validates the current <see cref="BufferDesc"/> instance.
    /// </summary>
    /// <returns><c>true</c> if valid; otherwise, <c>false</c>.</returns>
    public readonly bool Validate()
    {
        if (SizeInBytes is 0)
        {
            return false;
        }

        if (StructureStrideInBytes is 0)
        {
            return false;
        }

        if (Flags is BufferUsageFlags.None)
        {
            return false;
        }

        return true;
    }
}
