namespace Zenith.NET;

public record struct SamplerDesc : IDesc
{
    public SamplerDesc()
    {
        U = AddressMode.Wrap;
        V = AddressMode.Wrap;
        W = AddressMode.Wrap;
        Filter = Filter.MinPointMagPointMipPoint;
        ComparisonFunc = ComparisonFunc.Never;
        MaxAnisotropy = 0;
        MinLod = 0.0f;
        MaxLod = float.MaxValue;
        LodBias = 0.0f;
        BorderColor = BorderColor.TransparentBlack;
    }

    /// <summary>
    /// Mode to use for the U (or S) coordinate.
    /// </summary>
    public AddressMode U { get; set; }

    /// <summary>
    /// Mode to use for the V (or T) coordinate.
    /// </summary>
    public AddressMode V { get; set; }

    /// <summary>
    /// Mode to use for the W (or R) coordinate.
    /// </summary>
    public AddressMode W { get; set; }

    /// <summary>
    /// The filter used when sampling.
    /// </summary>
    public Filter Filter { get; set; }

    /// <summary>
    /// A function that compares sampled data against existing sampled data.
    /// </summary>
    public ComparisonFunc ComparisonFunc { get; set; }

    /// <summary>
    /// The maximum anisotropy of the filter.
    /// </summary>
    public uint MaxAnisotropy { get; set; }

    /// <summary>
    /// The minimum level of detail.
    /// </summary>
    public float MinLod { get; set; }

    /// <summary>
    /// The maximum level of detail.
    /// </summary>
    public float MaxLod { get; set; }

    /// <summary>
    /// The level of detail bias.
    /// </summary>
    public float LodBias { get; set; }

    /// <summary>
    /// The border color to use when sampling outside the texture.
    /// </summary>
    public BorderColor BorderColor { get; set; }

    public readonly bool Validate()
    {
        return MinLod <= MaxLod;
    }
}
