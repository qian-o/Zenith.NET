namespace Zenith.NET;

public record struct RayTracingGeometry
{
    public RayTracingGeometryType Type;

    public RayTracingTriangles Triangles;

    public RayTracingAABBs AABBs;

    public RayTracingGeometryFlags Flags;
}
