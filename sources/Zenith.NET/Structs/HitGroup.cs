namespace Zenith.NET;

public record struct HitGroup
{
    public HitGroupType Type;

    public string Name;

    public string? AnyHit;

    public string? Intersection;

    public string? ClosestHit;
}
