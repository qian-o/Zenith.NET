namespace Zenith.NET;

public struct BottomLevelAccelerationStructureDesc
{
    public IRayTracingGeometry[] Geometries { get; set; }

    public AccelerationStructureBuildFlags Flags { get; set; }
}
