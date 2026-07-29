namespace Zenith.NET;

public readonly struct SizeAndAlignment(ulong sizeInBytes, ulong alignmentInBytes)
{
    public readonly ulong SizeInBytes = sizeInBytes;

    public readonly ulong AlignmentInBytes = alignmentInBytes;
}
