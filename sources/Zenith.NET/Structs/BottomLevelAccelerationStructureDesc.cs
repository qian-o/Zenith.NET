using Zenith.NET.Helpers;

namespace Zenith.NET;

public record struct BottomLevelAccelerationStructureDesc : IDesc
{
    public BottomLevelAccelerationStructureDesc()
    {
        Geometries = [];
        Flags = AccelerationStructureBuildFlags.None;
    }

    public IRayTracingGeometryDesc[] Geometries { get; set; }

    public AccelerationStructureBuildFlags Flags { get; set; }

    public readonly bool Validate()
    {
        return Validation.IsAllValidDescs(Geometries);
    }
}
