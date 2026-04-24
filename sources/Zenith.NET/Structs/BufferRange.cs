namespace Zenith.NET;

public record struct BufferRange
{
    public Buffer Buffer;

    public uint OffsetInBytes;

    public uint SizeInBytes;

    public uint StrideInBytes;
}
