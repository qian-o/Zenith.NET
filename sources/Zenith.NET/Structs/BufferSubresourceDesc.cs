namespace Zenith.NET;

public record struct BufferSubresourceDesc
{
    public Buffer Buffer;

    public uint OffsetInBytes;

    public uint SizeInBytes;
}
