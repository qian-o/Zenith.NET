namespace Zenith.NET;

/// <summary>
/// Describes the blend state for the output-merger stage, including alpha-to-coverage, independent blending,
/// and per-render-target blend configurations.
/// </summary>
public record struct BlendStateDesc : IDesc
{
    /// <summary>
    /// Initializes a new instance of the <see cref="BlendStateDesc"/> struct with default values.
    /// </summary>
    public BlendStateDesc()
    {
        AlphaToCoverageEnable = false;
        IndependentBlendEnable = false;
        RenderTarget0 = new();
        RenderTarget1 = new();
        RenderTarget2 = new();
        RenderTarget3 = new();
        RenderTarget4 = new();
        RenderTarget5 = new();
        RenderTarget6 = new();
        RenderTarget7 = new();
    }

    /// <summary>
    /// Specifies whether to use alpha-to-coverage as a multisampling technique when setting a pixel to a render target.
    /// </summary>
    public bool AlphaToCoverageEnable { get; set; }

    /// <summary>
    /// Specifies whether to enable independent blending in simultaneous render targets.
    /// If set to <c>true</c>, each render target uses its own blend state. If <c>false</c>, only <see cref="RenderTarget0"/> is used.
    /// </summary>
    public bool IndependentBlendEnable { get; set; }

    /// <summary>
    /// Blend state description for render target 0.
    /// </summary>
    public BlendStateRenderTargetDesc RenderTarget0 { get; set; }

    /// <summary>
    /// Blend state description for render target 1.
    /// </summary>
    public BlendStateRenderTargetDesc RenderTarget1 { get; set; }

    /// <summary>
    /// Blend state description for render target 2.
    /// </summary>
    public BlendStateRenderTargetDesc RenderTarget2 { get; set; }

    /// <summary>
    /// Blend state description for render target 3.
    /// </summary>
    public BlendStateRenderTargetDesc RenderTarget3 { get; set; }

    /// <summary>
    /// Blend state description for render target 4.
    /// </summary>
    public BlendStateRenderTargetDesc RenderTarget4 { get; set; }

    /// <summary>
    /// Blend state description for render target 5.
    /// </summary>
    public BlendStateRenderTargetDesc RenderTarget5 { get; set; }

    /// <summary>
    /// Blend state description for render target 6.
    /// </summary>
    public BlendStateRenderTargetDesc RenderTarget6 { get; set; }

    /// <summary>
    /// Blend state description for render target 7.
    /// </summary>
    public BlendStateRenderTargetDesc RenderTarget7 { get; set; }

    /// <summary>
    /// Validates the current <see cref="BlendStateDesc"/> instance.
    /// </summary>
    /// <returns><c>true</c> if the descriptor is valid; otherwise, <c>false</c>.</returns>
    public readonly bool Validate()
    {
        return RenderTarget0.Validate()
               && RenderTarget1.Validate()
               && RenderTarget2.Validate()
               && RenderTarget3.Validate()
               && RenderTarget4.Validate()
               && RenderTarget5.Validate()
               && RenderTarget6.Validate()
               && RenderTarget7.Validate();
    }
}