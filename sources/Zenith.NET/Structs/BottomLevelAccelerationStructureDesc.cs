namespace Zenith.NET;

public record struct BottomLevelAccelerationStructureDesc
{
    public IRayTracingGeometry[] Geometries { get; set; }

    public AccelerationStructureBuildFlags Flags { get; set; }
}
