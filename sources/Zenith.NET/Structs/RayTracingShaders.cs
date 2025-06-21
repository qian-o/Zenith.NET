namespace Zenith.NET;

public record struct RayTracingShaders
{
    public Shader RayGeneration;

    public Shader[] Miss;

    public Shader[] AnyHit;

    public Shader[] Intersection;

    public Shader[] ClosestHit;
}
