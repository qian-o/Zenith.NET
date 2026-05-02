namespace Zenith.NET;

public record struct SamplerDesc
{
    public FilterMode MinFilter;

    public FilterMode MagFilter;

    public FilterMode MipmapFilter;

    public AddressMode AddressU;

    public AddressMode AddressV;

    public AddressMode AddressW;

    public CompareFunction CompareFunction;

    public uint MaxAnisotropy;

    public float LodBias;

    public float MinLod;

    public float MaxLod;

    public BorderColor BorderColor;
}
