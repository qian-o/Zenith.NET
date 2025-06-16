namespace Zenith.NET;

public struct RayTracingAABBs : IRayTracingGeometry
{
    public Buffer Buffer { get; set; }

    public uint Count { get; set; }

    public uint StrideInBytes { get; set; }

    public uint OffsetInBytes { get; set; }

    public RayTracingGeometryFlags Flags { get; set; }
}
