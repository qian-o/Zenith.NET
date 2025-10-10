namespace Zenith.NET;

public readonly struct MappedResource(nint data, uint sizeInBytes, uint rowPitch, uint slicePitch)
{
    public readonly nint Data = data;

    public readonly uint SizeInBytes = sizeInBytes;

    public readonly uint RowPitch = rowPitch;

    public readonly uint SlicePitch = slicePitch;
}
