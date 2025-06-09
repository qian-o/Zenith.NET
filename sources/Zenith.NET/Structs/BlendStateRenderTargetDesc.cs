namespace Zenith.NET;

/// <summary>
/// Describes the blend state for a single render target, including blend factors, blend operations, and write masks.
/// </summary>
public record struct BlendStateRenderTargetDesc : IDesc
{
    /// <summary>
    /// Initializes a new instance of the <see cref="BlendStateRenderTargetDesc"/> struct with default values.
    /// </summary>
    public BlendStateRenderTargetDesc()
    {
        BlendEnable = false;
        SrcBlend = Blend.One;
        DestBlend = Blend.Zero;
        BlendOp = BlendOp.Add;
        SrcBlendAlpha = Blend.One;
        DestBlendAlpha = Blend.Zero;
        BlendOpAlpha = BlendOp.Add;
        Flags = ColorComponentFlags.All;
    }

    /// <summary>
    /// Enables or disables blending for this render target.
    /// </summary>
    public bool BlendEnable { get; set; }

    /// <summary>
    /// The blend factor applied to the source color.
    /// </summary>
    public Blend SrcBlend { get; set; }

    /// <summary>
    /// The blend factor applied to the destination color.
    /// </summary>
    public Blend DestBlend { get; set; }

    /// <summary>
    /// The operation used to combine the source and destination colors.
    /// </summary>
    public BlendOp BlendOp { get; set; }

    /// <summary>
    /// The blend factor applied to the source alpha.
    /// </summary>
    public Blend SrcBlendAlpha { get; set; }

    /// <summary>
    /// The blend factor applied to the destination alpha.
    /// </summary>
    public Blend DestBlendAlpha { get; set; }

    /// <summary>
    /// The operation used to combine the source and destination alpha values.
    /// </summary>
    public BlendOp BlendOpAlpha { get; set; }

    /// <summary>
    /// Specifies which color components will be written to during rendering.
    /// </summary>
    public ColorComponentFlags Flags { get; set; }

    /// <summary>
    /// Validates the current <see cref="BlendStateRenderTargetDesc"/> instance.
    /// </summary>
    /// <returns><c>true</c> if valid; otherwise, <c>false</c>.</returns>
    public readonly bool Validate()
    {
        if (!Enum.IsDefined(SrcBlend))
        {
            return false;
        }

        if (!Enum.IsDefined(DestBlend))
        {
            return false;
        }

        if (!Enum.IsDefined(BlendOp))
        {
            return false;
        }

        if (!Enum.IsDefined(SrcBlendAlpha))
        {
            return false;
        }

        if (!Enum.IsDefined(DestBlendAlpha))
        {
            return false;
        }

        if (!Enum.IsDefined(BlendOpAlpha))
        {
            return false;
        }

        return true;
    }
}
