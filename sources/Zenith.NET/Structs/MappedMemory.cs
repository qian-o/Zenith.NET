namespace Zenith.NET;

public readonly record struct MappedMemory(nint Pointer, uint SizeInBytes, uint RowPitch, uint SlicePitch);
