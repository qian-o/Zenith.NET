using System.Numerics;

namespace Zenith.NET;

public record struct RayTracingInstanceDesc : IDesc
{
    public BottomLevelAccelerationStructure AccelerationStructure { get; set; }

    public uint InstanceID { get; set; }

    public byte InstanceMask { get; set; }

    public uint InstanceContributionToHitGroupIndex { get; set; }

    public Matrix4x4 Transform { get; set; }

    public RayTracingInstanceFlags Flags { get; set; }

    public readonly bool Validate()
    {
        return AccelerationStructure is not null;
    }
}
