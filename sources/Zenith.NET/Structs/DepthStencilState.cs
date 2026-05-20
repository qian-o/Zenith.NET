namespace Zenith.NET;

public struct DepthStencilState
{
    public bool IsDepthEnabled;

    public bool IsDepthWriteEnabled;

    public CompareOp DepthCompareOp;

    public bool IsStencilEnabled;

    public byte StencilReadMask;

    public byte StencilWriteMask;

    public StencilFaceState FrontFace;

    public StencilFaceState BackFace;

    public static DepthStencilState DepthReadWrite()
    {
        return new()
        {
            IsDepthEnabled = true,
            IsDepthWriteEnabled = true,
            DepthCompareOp = CompareOp.LessEqual,
            IsStencilEnabled = false,
            StencilReadMask = 0xFF,
            StencilWriteMask = 0xFF,
            FrontFace = StencilFaceState.Keep(),
            BackFace = StencilFaceState.Keep()
        };
    }

    public static DepthStencilState DepthReadWriteReverseZ()
    {
        return new()
        {
            IsDepthEnabled = true,
            IsDepthWriteEnabled = true,
            DepthCompareOp = CompareOp.GreaterEqual,
            IsStencilEnabled = false,
            StencilReadMask = 0xFF,
            StencilWriteMask = 0xFF,
            FrontFace = StencilFaceState.Keep(),
            BackFace = StencilFaceState.Keep()
        };
    }

    public static DepthStencilState DepthRead()
    {
        return new()
        {
            IsDepthEnabled = true,
            IsDepthWriteEnabled = false,
            DepthCompareOp = CompareOp.LessEqual,
            IsStencilEnabled = false,
            StencilReadMask = 0xFF,
            StencilWriteMask = 0xFF,
            FrontFace = StencilFaceState.Keep(),
            BackFace = StencilFaceState.Keep()
        };
    }

    public static DepthStencilState DepthReadReverseZ()
    {
        return new()
        {
            IsDepthEnabled = true,
            IsDepthWriteEnabled = false,
            DepthCompareOp = CompareOp.GreaterEqual,
            IsStencilEnabled = false,
            StencilReadMask = 0xFF,
            StencilWriteMask = 0xFF,
            FrontFace = StencilFaceState.Keep(),
            BackFace = StencilFaceState.Keep()
        };
    }

    public static DepthStencilState DepthNone()
    {
        return new()
        {
            IsDepthEnabled = false,
            IsDepthWriteEnabled = false,
            DepthCompareOp = CompareOp.LessEqual,
            IsStencilEnabled = false,
            StencilReadMask = 0xFF,
            StencilWriteMask = 0xFF,
            FrontFace = StencilFaceState.Keep(),
            BackFace = StencilFaceState.Keep()
        };
    }
}
