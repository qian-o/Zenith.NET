namespace Zenith.NET;

public record struct BufferDesc : IDesc
{
    public BufferDesc()
    {
        SizeInBytes = 0;
        StructureStrideInBytes = 0;
        Flags = BufferUsageFlags.None;
    }

    /// <summary>
    /// The desired capacity, in bytes.
    /// </summary>
    public uint SizeInBytes { get; set; }

    /// <summary>
    /// The byte stride of the structure.
    /// If the buffer is not structured, this should be set to 1.
    /// </summary>
    public uint StructureStrideInBytes { get; set; }

    /// <summary>
    /// Indicates the intended use of the buffer.
    /// </summary>
    public BufferUsageFlags Flags { get; set; }

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
