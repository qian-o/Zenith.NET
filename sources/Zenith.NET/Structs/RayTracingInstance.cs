using System.Numerics;

namespace Zenith.NET;

public record struct RayTracingInstance
{
    public BottomLevelAccelerationStructure AccelerationStructure;

    public uint ID;

    public byte Mask;

    public Matrix4x4 Transform;

    public RayTracingInstanceFlags Flags;
}
