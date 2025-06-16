namespace Zenith.NET;

public record struct RayTracingShadersDesc
{
    public Shader RayGeneration { get; set; }

    public Shader[] Miss { get; set; }

    public Shader[] AnyHit { get; set; }

    public Shader[] Intersection { get; set; }

    public Shader[] ClosestHit { get; set; }
}
