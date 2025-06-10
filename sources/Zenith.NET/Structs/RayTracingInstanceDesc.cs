using System.Numerics;

namespace Zenith.NET;

public record struct RayTracingInstanceDesc : IDesc
{
    public RayTracingInstanceDesc()
    {
        BLAS = null!;
        InstanceID = 0;
        InstanceMask = 0xFF;
        InstanceContributionToHitGroupIndex = 0;
        Transform = Matrix4x4.Identity;
        Flags = RayTracingInstanceFlags.None;
    }

    public BottomLevelAccelerationStructure BLAS { get; set; }

    public uint InstanceID { get; set; }

    public byte InstanceMask { get; set; }

    public uint InstanceContributionToHitGroupIndex { get; set; }

    public Matrix4x4 Transform { get; set; }

    public RayTracingInstanceFlags Flags { get; set; }

    public readonly bool Validate()
    {
        if (BLAS is null)
        {
            return false;
        }

        return true;
    }
}
