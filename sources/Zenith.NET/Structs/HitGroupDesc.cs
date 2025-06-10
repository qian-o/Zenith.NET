namespace Zenith.NET;

public record struct HitGroupDesc : IDesc
{
    public HitGroupDesc()
    {
        Type = HitGroupType.Triangles;
        Name = string.Empty;
        AnyHit = null;
        Intersection = null;
        ClosestHit = null;
    }

    public HitGroupType Type { get; set; }

    public string Name { get; set; }

    public string? AnyHit { get; set; }

    public string? Intersection { get; set; }

    public string? ClosestHit { get; set; }

    public readonly bool Validate()
    {
        if (!Enum.IsDefined(Type))
        {
            return false;
        }

        if (string.IsNullOrEmpty(Name))
        {
            return false;
        }

        return true;
    }
}
