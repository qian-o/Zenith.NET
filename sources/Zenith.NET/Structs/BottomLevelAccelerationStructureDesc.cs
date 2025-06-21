namespace Zenith.NET;

public record struct BottomLevelAccelerationStructureDesc
{
    public RayTracingGeometry[] Geometries;

    public AccelerationStructureBuildFlags Flags;
}
