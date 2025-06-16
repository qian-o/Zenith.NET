namespace Zenith.NET;

public record struct DepthStencilOpStateDesc
{
    public StencilOp StencilFailOp { get; set; }

    public StencilOp StencilDepthFailOp { get; set; }

    public StencilOp StencilPassOp { get; set; }

    public ComparisonFunc StencilFunc { get; set; }
}
