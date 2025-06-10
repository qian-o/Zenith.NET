namespace Zenith.NET;

[Flags]
public enum RayTracingGeometryFlags
{
    None = 0,

    Opaque = 1 << 0,

    NoDuplicateAnyHitInvocation = 1 << 1
}
