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
        if (Instances is null || Instances.Length is 0)
        {
            return false;
        }

        if (Instances.Any(static item => !item.Validate()))
        {
            return false;
        }

        return true;
    }
}
