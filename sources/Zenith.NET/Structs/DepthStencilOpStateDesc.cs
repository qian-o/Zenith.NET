namespace Zenith.NET;

public record struct DepthStencilOpStateDesc : IDesc
{
    public DepthStencilOpStateDesc()
    {
        StencilFailOp = StencilOp.Keep;
        StencilDepthFailOp = StencilOp.Keep;
        StencilPassOp = StencilOp.Keep;
        StencilFunc = ComparisonFunc.Always;
    }

    public StencilOp StencilFailOp { get; set; }

    public StencilOp StencilDepthFailOp { get; set; }

    public StencilOp StencilPassOp { get; set; }

    public ComparisonFunc StencilFunc { get; set; }

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
