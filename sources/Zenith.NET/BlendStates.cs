namespace Zenith.NET;

public static class BlendStates
{
    public static readonly BlendState Default = new()
    {
        IsAlphaToCoverageEnabled = false,
        IsIndependentBlendEnabled = false,
        ColorAttachment0 = new()
        {
            SourceRgbBlendFactor = BlendFactor.One,
            DestinationRgbBlendFactor = BlendFactor.Zero,
            RgbBlendOperation = BlendOperation.Add,
            SourceAlphaBlendFactor = BlendFactor.One,
            DestinationAlphaBlendFactor = BlendFactor.Zero,
            AlphaBlendOperation = BlendOperation.Add,
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
            SourceRgbBlendFactor = BlendFactor.SourceAlpha,
            DestinationRgbBlendFactor = BlendFactor.One,
            SourceAlphaBlendFactor = BlendFactor.SourceAlpha,
            DestinationAlphaBlendFactor = BlendFactor.One
        }
    };

    public static readonly BlendState AlphaBlend = new()
    {
        IsAlphaToCoverageEnabled = false,
        IsIndependentBlendEnabled = false,
        ColorAttachment0 = Default.ColorAttachment0 with
        {
            IsBlendingEnabled = true,
            SourceRgbBlendFactor = BlendFactor.One,
            DestinationRgbBlendFactor = BlendFactor.OneMinusSourceAlpha,
            SourceAlphaBlendFactor = BlendFactor.One,
            DestinationAlphaBlendFactor = BlendFactor.OneMinusSourceAlpha
        }
    };

    public static readonly BlendState NonPremultiplied = new()
    {
        IsAlphaToCoverageEnabled = false,
        IsIndependentBlendEnabled = false,
        ColorAttachment0 = Default.ColorAttachment0 with
        {
            IsBlendingEnabled = true,
            SourceRgbBlendFactor = BlendFactor.SourceAlpha,
            DestinationRgbBlendFactor = BlendFactor.OneMinusSourceAlpha,
            SourceAlphaBlendFactor = BlendFactor.SourceAlpha,
            DestinationAlphaBlendFactor = BlendFactor.OneMinusSourceAlpha
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
