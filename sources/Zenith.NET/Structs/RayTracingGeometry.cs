namespace Zenith.NET;

public struct RayTracingGeometry
{
    public RayTracingGeometryType Type;

    public RayTracingTriangleGeometry TriangleGeometry;

    public RayTracingAabbGeometry AabbGeometry;

    public bool IsOpaque;
}
