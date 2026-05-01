namespace Zenith.NET;

public static class DepthStencilStates
{
    public static readonly DepthStencilState Default = new()
    {
        DepthEnable = true,
        DepthWriteEnable = true,
        DepthCompare = CompareFunction.LessEqual,
        StencilEnable = false,
        StencilReadMask = 0xFF,
        StencilWriteMask = 0xFF,
        FrontFace = new()
        {
            Fail = StencilOperation.Keep,
            DepthFail = StencilOperation.Keep,
            Pass = StencilOperation.Keep,
            Compare = CompareFunction.Always
        },
        BackFace = new()
        {
            Fail = StencilOperation.Keep,
            DepthFail = StencilOperation.Keep,
            Pass = StencilOperation.Keep,
            Compare = CompareFunction.Always
        }
    };

    public static readonly DepthStencilState DefaultInverted = Default with
    {
        DepthCompare = CompareFunction.GreaterEqual
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
