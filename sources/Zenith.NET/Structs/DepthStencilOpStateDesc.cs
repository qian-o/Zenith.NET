namespace Zenith.NET;

/// <summary>
/// Describes the operations and comparison function to use for stencil testing in the depth-stencil stage.
/// </summary>
public record struct DepthStencilOpStateDesc : IDesc
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DepthStencilOpStateDesc"/> struct with default values.
    /// </summary>
    public DepthStencilOpStateDesc()
    {
        StencilFailOp = StencilOp.Keep;
        StencilDepthFailOp = StencilOp.Keep;
        StencilPassOp = StencilOp.Keep;
        StencilFunc = ComparisonFunc.Always;
    }

    /// <summary>
    /// The stencil operation to perform when stencil testing fails.
    /// </summary>
    public StencilOp StencilFailOp { get; set; }

    /// <summary>
    /// The stencil operation to perform when stencil testing passes and depth testing fails.
    /// </summary>
    public StencilOp StencilDepthFailOp { get; set; }

    /// <summary>
    /// The stencil operation to perform when both stencil and depth testing pass.
    /// </summary>
    public StencilOp StencilPassOp { get; set; }

    /// <summary>
    /// The comparison function used in the stencil test.
    /// </summary>
    public ComparisonFunc StencilFunc { get; set; }

    /// <summary>
    /// Validates the current <see cref="DepthStencilOpStateDesc"/> instance.
    /// </summary>
    /// <returns><c>true</c> if valid; otherwise, <c>false</c>.</returns>
    public readonly bool Validate()
    {
        if (!Enum.IsDefined(StencilFailOp))
        {
            return false;
        }

        if (!Enum.IsDefined(StencilDepthFailOp))
        {
            return false;
        }

        if (!Enum.IsDefined(StencilPassOp))
        {
            return false;
        }

        if (!Enum.IsDefined(StencilFunc))
        {
            return false;
        }

        return true;
    }
}
