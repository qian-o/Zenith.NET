namespace Zenith.NET;

public record struct TextureDataLayout
{
    public uint SizeInBytes;

    public uint RowPitchInBytes;

    public uint SlicePitchInBytes;
}