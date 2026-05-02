namespace Zenith.NET;

public static class DepthStencilStates
{
    public static readonly DepthStencilState Default = new()
    {
        IsDepthEnabled = true,
        IsDepthWriteEnabled = true,
        DepthCompareFunction = CompareFunction.LessEqual,
        IsStencilEnabled = false,
        StencilReadMask = 0xFF,
        StencilWriteMask = 0xFF,
        FrontFace = new()
        {
            FailOperation = StencilOperation.Keep,
            DepthFailOperation = StencilOperation.Keep,
            PassOperation = StencilOperation.Keep,
            CompareFunction = CompareFunction.Always
        },
        BackFace = new()
        {
            FailOperation = StencilOperation.Keep,
            DepthFailOperation = StencilOperation.Keep,
            PassOperation = StencilOperation.Keep,
            CompareFunction = CompareFunction.Always
        }
    };

    public static readonly DepthStencilState DefaultInverted = Default with
    {
        DepthCompareFunction = CompareFunction.GreaterEqual
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
