namespace Zenith.NET;

public readonly struct MappedMemory(nint pointer, uint sizeInBytes, uint rowPitch, uint slicePitch)
{
    public readonly nint Pointer = pointer;

    public readonly uint SizeInBytes = sizeInBytes;

    public readonly uint RowPitch = rowPitch;

    public readonly uint SlicePitch = slicePitch;
}
