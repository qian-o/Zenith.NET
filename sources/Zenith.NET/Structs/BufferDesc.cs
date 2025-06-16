namespace Zenith.NET;

public record struct BufferDesc
{
    public uint SizeInBytes { get; set; }

    public uint StrideInBytes { get; set; }

    public BufferUsageFlags Flags { get; set; }
}
