namespace Zenith.NET;

public struct RayTracingShaders
{
    public Shader RayGeneration { get; set; }

    public Shader[] Miss { get; set; }

    public Shader[] AnyHit { get; set; }

    public Shader[] Intersection { get; set; }

    public Shader[] ClosestHit { get; set; }
}
