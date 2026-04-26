namespace Zenith.NET;

public record struct SamplerDesc
{
    public Filter Filter;

    public AddressMode U;

    public AddressMode V;

    public AddressMode W;

    public ComparisonFunc ComparisonFunc;

    public uint MaxAnisotropy;

    public float LodBias;

    public float MinLod;

    public float MaxLod;

    public BorderColor BorderColor;
}
