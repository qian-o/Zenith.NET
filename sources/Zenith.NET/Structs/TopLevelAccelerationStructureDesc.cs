namespace Zenith.NET;

public record struct TopLevelAccelerationStructureDesc
{
    public RayTracingInstance[] Instances;

    public AccelerationStructureBuildFlags Flags;
}
