namespace Zenith.NET;

public static class BlendStates
{
    public static readonly BlendState Default = new()
    {
        AlphaToCoverageEnable = false,
        IndependentBlendEnable = false,
        RenderTarget0 = new()
        {
            SrcFactor = BlendFactor.One,
            DstFactor = BlendFactor.Zero,
            Operation = BlendOperation.Add,
            SrcFactorAlpha = BlendFactor.One,
            DstFactorAlpha = BlendFactor.Zero,
            OperationAlpha = BlendOperation.Add,
            Writes = ColorWrites.All
        }
    };

    public static readonly BlendState Additive = new()
    {
        RenderTarget0 = Default.RenderTarget0 with
        {
            BlendEnable = true,
            SrcFactor = BlendFactor.SrcAlpha,
            DstFactor = BlendFactor.One,
            SrcFactorAlpha = BlendFactor.SrcAlpha,
            DstFactorAlpha = BlendFactor.One
        }
    };

    public static readonly BlendState AlphaBlend = new()
    {
        RenderTarget0 = Default.RenderTarget0 with
        {
            BlendEnable = true,
            SrcFactor = BlendFactor.One,
            DstFactor = BlendFactor.OneMinusSrcAlpha,
            SrcFactorAlpha = BlendFactor.One,
            DstFactorAlpha = BlendFactor.OneMinusSrcAlpha
        }
    };

    public static readonly BlendState NonPremultiplied = new()
    {
        RenderTarget0 = Default.RenderTarget0 with
        {
            BlendEnable = true,
            SrcFactor = BlendFactor.SrcAlpha,
            DstFactor = BlendFactor.OneMinusSrcAlpha,
            SrcFactorAlpha = BlendFactor.SrcAlpha,
            DstFactorAlpha = BlendFactor.OneMinusSrcAlpha
        }
    };

    public static readonly BlendState Opaque = new()
    {
        RenderTarget0 = Default.RenderTarget0 with
        {
            BlendEnable = false
        }
    };

    public static readonly BlendState ColorDisabled = new()
    {
        RenderTarget0 = Default.RenderTarget0 with
        {
            BlendEnable = false,
            Writes = ColorWrites.None
        }
    };
}
