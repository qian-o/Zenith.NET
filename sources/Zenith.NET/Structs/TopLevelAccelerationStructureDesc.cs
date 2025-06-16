namespace Zenith.NET;

public record struct TopLevelAccelerationStructureDesc
{
    public RayTracingInstanceDesc[] Instances { get; set; }

    public AccelerationStructureBuildFlags Flags { get; set; }
}
