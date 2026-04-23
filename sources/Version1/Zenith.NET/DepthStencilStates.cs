namespace Zenith.NET;

public static class DepthStencilStates
{
    public static readonly DepthStencilState Default = new()
    {
        DepthEnable = true,
        DepthWriteEnable = true,
        DepthFunc = ComparisonFunc.LessEqual,
        StencilEnable = false,
        StencilReadMask = 0xFF,
        StencilWriteMask = 0xFF,
        FrontFace = new()
        {
            StencilFailOp = StencilOp.Keep,
            StencilDepthFailOp = StencilOp.Keep,
            StencilPassOp = StencilOp.Keep,
            StencilFunc = ComparisonFunc.Always
        },
        BackFace = new()
        {
            StencilFailOp = StencilOp.Keep,
            StencilDepthFailOp = StencilOp.Keep,
            StencilPassOp = StencilOp.Keep,
            StencilFunc = ComparisonFunc.Always
        }
    };

    public static readonly DepthStencilState DefaultInverted = Default with
    {
        DepthFunc = ComparisonFunc.GreaterEqual
    };

    public static readonly DepthStencilState DepthRead = Default with
    {
        DepthWriteEnable = false
    };

    public static readonly DepthStencilState None = Default with
    {
        DepthEnable = false,
        DepthWriteEnable = false
    };
}
