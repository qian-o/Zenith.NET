using Zenith.NET.Helpers;

namespace Zenith.NET;

public record struct TopLevelAccelerationStructureDesc : IDesc
{
    public RayTracingInstanceDesc[] Instances { get; set; }

    public AccelerationStructureBuildFlags Flags { get; set; }

    public readonly bool Validate()
    {
        return Validation.IsValidDescs(Instances);
    }
}
