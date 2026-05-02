namespace Zenith.NET;

public record struct RayTracingGeometry
{
    public RayTracingGeometryType Type;

    public RayTracingTriangleGeometry TriangleGeometry;

    public RayTracingAabbGeometry AabbGeometry;

    public bool IsOpaque;
}
