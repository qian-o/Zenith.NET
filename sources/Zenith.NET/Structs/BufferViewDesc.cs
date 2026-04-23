namespace Zenith.NET;

public record struct BufferViewDesc
{
    public Buffer Buffer;

    public uint OffsetInBytes;

    public uint SizeInBytes;

    public uint StrideInBytes;
}
