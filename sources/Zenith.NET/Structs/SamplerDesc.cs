namespace Zenith.NET;

public record struct SamplerDesc : IDesc
{
    public AddressMode U { get; set; }

    public AddressMode V { get; set; }

    public AddressMode W { get; set; }

    public Filter Filter { get; set; }

    public ComparisonFunc ComparisonFunc { get; set; }

    public uint MaxAnisotropy { get; set; }

    public float MinLod { get; set; }

    public float MaxLod { get; set; }

    public float LodBias { get; set; }

    public BorderColor BorderColor { get; set; }

    public readonly bool Validate()
    {
        if (!Enum.IsDefined(U))
        {
            return false;
        }

        if (!Enum.IsDefined(V))
        {
            return false;
        }

        if (!Enum.IsDefined(W))
        {
            return false;
        }

        if (!Enum.IsDefined(Filter))
        {
            return false;
        }

        if (!Enum.IsDefined(ComparisonFunc))
        {
            return false;
        }

        if (MinLod > MaxLod)
        {
            return false;
        }

        return true;
    }
}
