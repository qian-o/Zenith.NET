using System.Numerics;

namespace Zenith.NET;

/// <summary>
/// Describes the complete render state, including rasterizer, depth-stencil, and blend state,
/// as well as stencil reference and blend factor for a draw call or pipeline state object.
/// </summary>
public record struct RenderStateDesc : IDesc
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RenderStateDesc"/> struct with default values.
    /// </summary>
    public RenderStateDesc()
    {
        RasterizerState = new();
        DepthStencilState = new();
        BlendState = new();
        StencilReference = 0;
        BlendFactor = null;
    }

    /// <summary>
    /// The rasterizer state, controlling how primitives are converted to pixels.
    /// </summary>
    public RasterizerStateDesc RasterizerState { get; set; }

    /// <summary>
    /// The depth-stencil state, controlling depth and stencil testing and operations.
    /// </summary>
    public DepthStencilStateDesc DepthStencilState { get; set; }

    /// <summary>
    /// The blend state, controlling blending operations for render targets.
    /// </summary>
    public BlendStateDesc BlendState { get; set; }

    /// <summary>
    /// The reference value used for stencil testing.
    /// </summary>
    public int StencilReference { get; set; }

    /// <summary>
    /// The blend factor used for blend operations, or <c>null</c> to use the default.
    /// </summary>
    public Vector4? BlendFactor { get; set; }

    /// <summary>
    /// Validates the current <see cref="RenderStateDesc"/> instance.
    /// </summary>
    /// <returns><c>true</c> if valid; otherwise, <c>false</c>.</returns>
    public readonly bool Validate()
    {
        if (!RasterizerState.Validate())
        {
            return false;
        }

        if (!DepthStencilState.Validate())
        {
            return false;
        }

        if (!BlendState.Validate())
        {
            return false;
        }

        return true;
    }
}
