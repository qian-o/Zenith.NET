namespace Zenith.NET;

public record struct DepthStencilStateOp
{
    public StencilOp StencilFailOp;

    public StencilOp StencilDepthFailOp;

    public StencilOp StencilPassOp;

    public ComparisonFunc StencilFunc;
}
