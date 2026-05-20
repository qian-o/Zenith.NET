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

    public static readonly DepthStencilState DefaultInverted = new()
    {
        IsDepthEnabled = true,
        IsDepthWriteEnabled = true,
        DepthCompareOp = CompareOp.GreaterEqual,
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

    public static readonly DepthStencilState DepthRead = new()
    {
        IsDepthEnabled = true,
        IsDepthWriteEnabled = false,
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

    public static readonly DepthStencilState None = new()
    {
        IsDepthEnabled = false,
        IsDepthWriteEnabled = false,
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
}
