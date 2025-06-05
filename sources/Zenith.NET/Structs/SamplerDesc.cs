namespace Zenith.NET;

/// <summary>
/// Describes a sampler state, including addressing modes, filtering, comparison function, and LOD settings.
/// </summary>
public record struct SamplerDesc : IDesc
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SamplerDesc"/> struct with default values.
    /// </summary>
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
    /// The addressing mode for the U (or S) coordinate.
    /// </summary>
    public AddressMode U { get; set; }

    /// <summary>
    /// The addressing mode for the V (or T) coordinate.
    /// </summary>
    public AddressMode V { get; set; }

    /// <summary>
    /// The addressing mode for the W (or R) coordinate.
    /// </summary>
    public AddressMode W { get; set; }

    /// <summary>
    /// The filter used when sampling.
    /// </summary>
    public Filter Filter { get; set; }

    /// <summary>
    /// The comparison function used for comparison sampling.
    /// </summary>
    public ComparisonFunc ComparisonFunc { get; set; }

    /// <summary>
    /// The maximum anisotropy for anisotropic filtering.
    /// </summary>
    public uint MaxAnisotropy { get; set; }

    /// <summary>
    /// The minimum level of detail (LOD).
    /// </summary>
    public float MinLod { get; set; }

    /// <summary>
    /// The maximum level of detail (LOD).
    /// </summary>
    public float MaxLod { get; set; }

    /// <summary>
    /// The level of detail (LOD) bias.
    /// </summary>
    public float LodBias { get; set; }

    /// <summary>
    /// The border color used when sampling outside the texture.
    /// </summary>
    public BorderColor BorderColor { get; set; }

    /// <summary>
    /// Validates the current <see cref="SamplerDesc"/> instance.
    /// </summary>
    /// <returns><c>true</c> if valid; otherwise, <c>false</c>.</returns>
    public readonly bool Validate()
    {
        return MinLod <= MaxLod;
    }
}
