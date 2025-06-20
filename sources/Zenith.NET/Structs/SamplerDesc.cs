namespace Zenith.NET;

public record struct SamplerDesc
{
    public AddressMode U;

    public AddressMode V;

    public AddressMode W;

    public Filter Filter;

    public ComparisonFunc ComparisonFunc;

    public uint MaxAnisotropy;

    public float MinLod;

    public float MaxLod;

    public float LodBias;

    public BorderColor BorderColor;
}
