namespace Zenith.NET;

[Flags]
public enum RayTracingInstanceFlags
{
    None = 0,

    CullDisable = 1 << 0,

    FrontCounterClockwise = 1 << 1,

    ForceOpaque = 1 << 2,

    ForceNoOpaque = 1 << 3
}
