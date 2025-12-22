namespace Zenith.NET;

public readonly record struct MappedMemory(nint Pointer, uint SizeInBytes);
