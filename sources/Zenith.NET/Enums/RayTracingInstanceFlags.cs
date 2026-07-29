namespace Zenith.NET;

[Flags]
public enum RayTracingInstanceFlags
{
    None = 0,

    FrontCounterClockwise = 1 << 0,

    DisableCull = 1 << 1,

    ForceOpaque = 1 << 2,

    ForceNonOpaque = 1 << 3
}
