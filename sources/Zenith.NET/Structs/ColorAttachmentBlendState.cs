namespace Zenith.NET;

public struct ColorAttachmentBlendState
{
    public bool IsBlendingEnabled;

    public BlendFactor SrcRgbFactor;

    public BlendFactor DstRgbFactor;

    public BlendOp RgbOp;

    public BlendFactor SrcAlphaFactor;

    public BlendFactor DstAlphaFactor;

    public BlendOp AlphaOp;

    public ColorWrites ColorWrites;

    public static ColorAttachmentBlendState Opaque()
    {
        return new()
        {
            IsBlendingEnabled = false,
            SrcRgbFactor = BlendFactor.One,
            DstRgbFactor = BlendFactor.Zero,
            RgbOp = BlendOp.Add,
            SrcAlphaFactor = BlendFactor.One,
            DstAlphaFactor = BlendFactor.Zero,
            AlphaOp = BlendOp.Add,
            ColorWrites = ColorWrites.All
        };
    }

    public static ColorAttachmentBlendState AlphaBlend()
    {
        return new()
        {
            IsBlendingEnabled = true,
            SrcRgbFactor = BlendFactor.One,
            DstRgbFactor = BlendFactor.OneMinusSourceAlpha,
            RgbOp = BlendOp.Add,
            SrcAlphaFactor = BlendFactor.One,
            DstAlphaFactor = BlendFactor.OneMinusSourceAlpha,
            AlphaOp = BlendOp.Add,
            ColorWrites = ColorWrites.All
        };
    }

    public static ColorAttachmentBlendState Additive()
    {
        return new()
        {
            IsBlendingEnabled = true,
            SrcRgbFactor = BlendFactor.SourceAlpha,
            DstRgbFactor = BlendFactor.One,
            RgbOp = BlendOp.Add,
            SrcAlphaFactor = BlendFactor.SourceAlpha,
            DstAlphaFactor = BlendFactor.One,
            AlphaOp = BlendOp.Add,
            ColorWrites = ColorWrites.All
        };
    }

    public static ColorAttachmentBlendState NonPremultiplied()
    {
        return new()
        {
            IsBlendingEnabled = true,
            SrcRgbFactor = BlendFactor.SourceAlpha,
            DstRgbFactor = BlendFactor.OneMinusSourceAlpha,
            RgbOp = BlendOp.Add,
            SrcAlphaFactor = BlendFactor.SourceAlpha,
            DstAlphaFactor = BlendFactor.OneMinusSourceAlpha,
            AlphaOp = BlendOp.Add,
            ColorWrites = ColorWrites.All
        };
    }

    public static ColorAttachmentBlendState ColorDisabled()
    {
        return new()
        {
            IsBlendingEnabled = false,
            SrcRgbFactor = BlendFactor.One,
            DstRgbFactor = BlendFactor.Zero,
            RgbOp = BlendOp.Add,
            SrcAlphaFactor = BlendFactor.One,
            DstAlphaFactor = BlendFactor.Zero,
            AlphaOp = BlendOp.Add,
            ColorWrites = ColorWrites.None
        };
    }
}