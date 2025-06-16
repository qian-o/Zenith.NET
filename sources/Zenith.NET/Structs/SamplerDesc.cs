namespace Zenith.NET;

public struct SamplerDesc
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
}
