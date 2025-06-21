namespace Zenith.NET;

[Flags]
public enum RayTracingInstanceFlags
{
    None = 0,

    TriangleCullDisable = 1 << 0,

    TriangleFrontCounterClockwise = 1 << 1,

    ForceOpaque = 1 << 2,

    ForceNoOpaque = 1 << 3
}
