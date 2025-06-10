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
        if (Geometries is null || Geometries.Length is 0)
        {
            return false;
        }

        if (Geometries.Any(static item => !item.Validate()))
        {
            return false;
        }

        return true;
    }
}
