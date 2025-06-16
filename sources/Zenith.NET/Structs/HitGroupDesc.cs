namespace Zenith.NET;

public record struct HitGroupDesc
{
    public HitGroupType Type { get; set; }

    public string Name { get; set; }

    public string? AnyHit { get; set; }

    public string? Intersection { get; set; }

    public string? ClosestHit { get; set; }
}
