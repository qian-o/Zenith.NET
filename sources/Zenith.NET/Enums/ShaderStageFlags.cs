namespace Zenith.NET;

[Flags]
public enum ShaderStageFlags
{
    None = 0,

    Vertex = 1 << 0,

    Hull = 1 << 1,

    Domain = 1 << 2,

    Geometry = 1 << 3,

    Pixel = 1 << 4,

    Compute = 1 << 5,

    RayGeneration = 1 << 6,

    Miss = 1 << 7,

    ClosestHit = 1 << 8,

    AnyHit = 1 << 9,

    Intersection = 1 << 10,

    Callable = 1 << 11
}