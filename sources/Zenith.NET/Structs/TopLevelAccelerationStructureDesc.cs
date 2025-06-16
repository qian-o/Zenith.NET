namespace Zenith.NET;

public struct TopLevelAccelerationStructureDesc
{
    public RayTracingInstance[] Instances { get; set; }

    public AccelerationStructureBuildFlags Flags { get; set; }
}
