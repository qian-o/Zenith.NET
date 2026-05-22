namespace Zenith.NET;

public readonly struct ResourceSizeAndAlignment(ulong sizeInBytes, ulong alignmentInBytes)
{
    public readonly ulong SizeInBytes = sizeInBytes;

    public readonly ulong AlignmentInBytes = alignmentInBytes;
}
