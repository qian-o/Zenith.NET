using System.Numerics;

namespace Zenith.NET;

public struct RayTracingInstance
{
    public BottomLevelAccelerationStructure AccelerationStructure;

    public uint InstanceId;

    public byte VisibilityMask;

    public Matrix4x4 Transform;

    public RayTracingInstanceFlags Flags;
}
