namespace Zenith.NET;

public record struct RayTracingShadersDesc : IDesc
{
    public RayTracingShadersDesc()
    {
        RayGeneration = null!;
        Miss = [];
        AnyHit = [];
        Intersection = [];
        ClosestHit = [];
    }

    public Shader RayGeneration { get; set; }

    public Shader[] Miss { get; set; }

    public Shader[] AnyHit { get; set; }

    public Shader[] Intersection { get; set; }

    public Shader[] ClosestHit { get; set; }

    public readonly bool Validate()
    {
        return RayGeneration is not null;
    }
}
