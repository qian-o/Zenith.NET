namespace Zenith.NET;

public readonly struct MappedMemory
{
    public nint Pointer { get; init; }

    public uint SizeInBytes { get; init; }
}
