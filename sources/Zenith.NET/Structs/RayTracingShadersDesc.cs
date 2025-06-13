namespace Zenith.NET;

public record struct RayTracingShadersDesc : IDesc
{
    public Shader RayGeneration { get; set; }

    public Shader[] Miss { get; set; }

    public Shader[] AnyHit { get; set; }

    public Shader[] Intersection { get; set; }

    public Shader[] ClosestHit { get; set; }

    public readonly bool Validate()
    {
        if (RayGeneration is null)
        {
            return false;
        }

        if (Miss is null)
        {
            return false;
        }

        if (AnyHit is null)
        {
            return false;
        }

        if (Intersection is null)
        {
            return false;
        }

        if (ClosestHit is null)
        {
            return false;
        }

        return true;
    }
}
