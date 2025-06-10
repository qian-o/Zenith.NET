namespace Zenith.NET;

public interface IRayTracingGeometryDesc : IDesc
{
    RayTracingGeometryFlags Flags { get; set; }
}
