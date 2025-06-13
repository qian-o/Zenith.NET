namespace Zenith.NET;

public record struct BufferDesc : IDesc
{
    public uint SizeInBytes { get; set; }

    public uint StrideInBytes { get; set; }

    public BufferUsageFlags Flags { get; set; }

    public readonly bool Validate()
    {
        if (SizeInBytes is 0)
        {
            return false;
        }

        if (StrideInBytes is 0)
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
