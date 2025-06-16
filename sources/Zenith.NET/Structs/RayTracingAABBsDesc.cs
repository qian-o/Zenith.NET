namespace Zenith.NET;

public record struct RayTracingAABBsDesc : IRayTracingGeometry
{
    public Buffer Buffer { get; set; }

    public uint Count { get; set; }

    public uint StrideInBytes { get; set; }

    public uint OffsetInBytes { get; set; }

    public RayTracingGeometryFlags Flags { get; set; }
}
