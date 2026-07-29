namespace Zenith.NET;

public struct RayTracingGeometry
{
    public RayTracingGeometryType Type;

    public RayTracingTriangleGeometry TriangleGeometry;

    public RayTracingAabbGeometry AabbGeometry;

    public bool IsOpaque;

    public static RayTracingGeometry Triangles(RayTracingTriangleGeometry geometry, bool isOpaque)
    {
        return new()
        {
            Type = RayTracingGeometryType.Triangle,
            TriangleGeometry = geometry,
            AabbGeometry = new(),
            IsOpaque = isOpaque
        };
    }

    public static RayTracingGeometry Aabbs(RayTracingAabbGeometry geometry, bool isOpaque)
    {
        return new()
        {
            Type = RayTracingGeometryType.Aabb,
            TriangleGeometry = new(),
            AabbGeometry = geometry,
            IsOpaque = isOpaque
        };
    }
}
