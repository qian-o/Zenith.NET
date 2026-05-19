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
        ColorAttachment0 = Default.ColorAttachment0 with
        {
            IsBlendingEnabled = true,
            SrcRgbFactor = BlendFactor.SourceAlpha,
            DstRgbFactor = BlendFactor.One,
            SrcAlphaFactor = BlendFactor.SourceAlpha,
            DstAlphaFactor = BlendFactor.One
        }
    };

    public static readonly BlendState AlphaBlend = new()
    {
        IsAlphaToCoverageEnabled = false,
        IsIndependentBlendEnabled = false,
        ColorAttachment0 = Default.ColorAttachment0 with
        {
            IsBlendingEnabled = true,
            SrcRgbFactor = BlendFactor.One,
            DstRgbFactor = BlendFactor.OneMinusSourceAlpha,
            SrcAlphaFactor = BlendFactor.One,
            DstAlphaFactor = BlendFactor.OneMinusSourceAlpha
        }
    };

    public static readonly BlendState NonPremultiplied = new()
    {
        IsAlphaToCoverageEnabled = false,
        IsIndependentBlendEnabled = false,
        ColorAttachment0 = Default.ColorAttachment0 with
        {
            IsBlendingEnabled = true,
            SrcRgbFactor = BlendFactor.SourceAlpha,
            DstRgbFactor = BlendFactor.OneMinusSourceAlpha,
            SrcAlphaFactor = BlendFactor.SourceAlpha,
            DstAlphaFactor = BlendFactor.OneMinusSourceAlpha
        }
    };

    public static readonly BlendState Opaque = new()
    {
        IsAlphaToCoverageEnabled = false,
        IsIndependentBlendEnabled = false,
        ColorAttachment0 = Default.ColorAttachment0 with
        {
            IsBlendingEnabled = false
        }
    };

    public static readonly BlendState ColorDisabled = new()
    {
        IsAlphaToCoverageEnabled = false,
        IsIndependentBlendEnabled = false,
        ColorAttachment0 = Default.ColorAttachment0 with
        {
            IsBlendingEnabled = false,
            ColorWrites = ColorWrites.None
        }
    };
}
