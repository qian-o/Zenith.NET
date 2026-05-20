namespace Zenith.NET;

public static class BlendStates
{
    public static readonly BlendState Default = new()
    {
        IsAlphaToCoverageEnabled = false,
        IsIndependentBlendEnabled = false,
        ColorAttachment0 = new()
        {
            SrcRgbFactor = BlendFactor.One,
            DstRgbFactor = BlendFactor.Zero,
            RgbOp = BlendOp.Add,
            SrcAlphaFactor = BlendFactor.One,
            DstAlphaFactor = BlendFactor.Zero,
            AlphaOp = BlendOp.Add,
            ColorWrites = ColorWrites.All
        }
    };

    public static readonly BlendState Additive = new()
    {
        IsAlphaToCoverageEnabled = false,
        IsIndependentBlendEnabled = false,
        ColorAttachment0 = new()
        {
            IsBlendingEnabled = true,
            SrcRgbFactor = BlendFactor.SourceAlpha,
            DstRgbFactor = BlendFactor.One,
            RgbOp = BlendOp.Add,
            SrcAlphaFactor = BlendFactor.SourceAlpha,
            DstAlphaFactor = BlendFactor.One,
            AlphaOp = BlendOp.Add,
            ColorWrites = ColorWrites.All
        }
    };

    public static readonly BlendState AlphaBlend = new()
    {
        IsAlphaToCoverageEnabled = false,
        IsIndependentBlendEnabled = false,
        ColorAttachment0 = new()
        {
            IsBlendingEnabled = true,
            SrcRgbFactor = BlendFactor.One,
            DstRgbFactor = BlendFactor.OneMinusSourceAlpha,
            RgbOp = BlendOp.Add,
            SrcAlphaFactor = BlendFactor.One,
            DstAlphaFactor = BlendFactor.OneMinusSourceAlpha,
            AlphaOp = BlendOp.Add,
            ColorWrites = ColorWrites.All
        }
    };

    public static readonly BlendState NonPremultiplied = new()
    {
        IsAlphaToCoverageEnabled = false,
        IsIndependentBlendEnabled = false,
        ColorAttachment0 = new()
        {
            IsBlendingEnabled = true,
            SrcRgbFactor = BlendFactor.SourceAlpha,
            DstRgbFactor = BlendFactor.OneMinusSourceAlpha,
            RgbOp = BlendOp.Add,
            SrcAlphaFactor = BlendFactor.SourceAlpha,
            DstAlphaFactor = BlendFactor.OneMinusSourceAlpha,
            AlphaOp = BlendOp.Add,
            ColorWrites = ColorWrites.All
        }
    };

    public static readonly BlendState Opaque = new()
    {
        IsAlphaToCoverageEnabled = false,
        IsIndependentBlendEnabled = false,
        ColorAttachment0 = new()
        {
            IsBlendingEnabled = false,
            SrcRgbFactor = BlendFactor.One,
            DstRgbFactor = BlendFactor.Zero,
            RgbOp = BlendOp.Add,
            SrcAlphaFactor = BlendFactor.One,
            DstAlphaFactor = BlendFactor.Zero,
            AlphaOp = BlendOp.Add,
            ColorWrites = ColorWrites.All
        }
    };

    public static readonly BlendState ColorDisabled = new()
    {
        IsAlphaToCoverageEnabled = false,
        IsIndependentBlendEnabled = false,
        ColorAttachment0 = new()
        {
            IsBlendingEnabled = false,
            SrcRgbFactor = BlendFactor.One,
            DstRgbFactor = BlendFactor.Zero,
            RgbOp = BlendOp.Add,
            SrcAlphaFactor = BlendFactor.One,
            DstAlphaFactor = BlendFactor.Zero,
            AlphaOp = BlendOp.Add,
            ColorWrites = ColorWrites.None
        }
    };
}
