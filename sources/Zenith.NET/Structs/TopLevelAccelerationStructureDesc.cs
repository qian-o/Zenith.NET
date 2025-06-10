using Zenith.NET.Helpers;

namespace Zenith.NET;

public record struct TopLevelAccelerationStructureDesc : IDesc
{
    public TopLevelAccelerationStructureDesc()
    {
        Instances = [];
        Flags = AccelerationStructureBuildFlags.None;
    }

    public RayTracingInstanceDesc[] Instances { get; set; }

    public AccelerationStructureBuildFlags Flags { get; set; }

    public readonly bool Validate()
    {
        return Validation.IsAllValidDescs(Instances);
    }
}
