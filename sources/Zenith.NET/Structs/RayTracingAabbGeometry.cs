namespace Zenith.NET;

public record struct RayTracingAabbGeometry
{
    public Buffer Buffer;

    public uint Count;

    public uint StrideInBytes;

    public uint OffsetInBytes;
}