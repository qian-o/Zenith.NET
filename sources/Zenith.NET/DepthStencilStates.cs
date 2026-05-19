namespace Zenith.NET;

public static class DepthStencilStates
{
    public static readonly DepthStencilState Default = new()
    {
        IsDepthEnabled = true,
        IsDepthWriteEnabled = true,
        DepthCompareOp = CompareOp.LessEqual,
        IsStencilEnabled = false,
        StencilReadMask = 0xFF,
        StencilWriteMask = 0xFF,
        FrontFace = new()
        {
            FailOp = StencilOp.Keep,
            DepthFailOp = StencilOp.Keep,
            PassOp = StencilOp.Keep,
            CompareOp = CompareOp.Always
        },
        BackFace = new()
        {
            FailOp = StencilOp.Keep,
            DepthFailOp = StencilOp.Keep,
            PassOp = StencilOp.Keep,
            CompareOp = CompareOp.Always
        }
    };

    public static readonly DepthStencilState DefaultInverted = Default with
    {
        DepthCompareOp = CompareOp.GreaterEqual
    };

    public static readonly DepthStencilState DepthRead = Default with
    {
        IsDepthWriteEnabled = false
    };

    public static readonly DepthStencilState None = Default with
    {
        IsDepthEnabled = false,
        IsDepthWriteEnabled = false
    };
}
