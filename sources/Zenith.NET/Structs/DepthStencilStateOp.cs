namespace Zenith.NET;

public struct DepthStencilStateOp
{
    public StencilOp StencilFailOp { get; set; }

    public StencilOp StencilDepthFailOp { get; set; }

    public StencilOp StencilPassOp { get; set; }

    public ComparisonFunc StencilFunc { get; set; }
}
