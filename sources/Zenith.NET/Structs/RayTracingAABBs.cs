namespace Zenith.NET;

public record struct RayTracingAABBs
{
    public Buffer Buffer;

    public uint Count;

    public uint StrideInBytes;

    public uint OffsetInBytes;

    public RayTracingGeometryFlags Flags;
}
