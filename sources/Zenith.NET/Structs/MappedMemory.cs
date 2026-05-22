namespace Zenith.NET;

public readonly struct MappedMemory(nint pointer, uint sizeInBytes)
{
    public readonly nint Pointer = pointer;

    public readonly uint SizeInBytes = sizeInBytes;
}
