namespace Zenith.NET;

public record struct TextureDataLayout
{
    public uint SizeInBytes;

    public uint RowStrideInBytes;

    public uint SliceStrideInBytes;
}