namespace Zenith.NET;

public record struct RayTracingAABBs
{
    public IBuffer Buffer;

    public uint Count;

    public uint StrideInBytes;

    public uint OffsetInBytes;

    public RayTracingGeometryFlags Flags;
}
