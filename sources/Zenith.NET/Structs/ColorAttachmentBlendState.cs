namespace Zenith.NET;

public record struct ColorAttachmentBlendState
{
    public bool IsBlendingEnabled;

    public BlendFactor SrcRgbFactor;

    public BlendFactor DstRgbFactor;

    public BlendOp RgbOp;

    public BlendFactor SrcAlphaFactor;

    public BlendFactor DstAlphaFactor;

    public BlendOp AlphaOp;

    public ColorWrites ColorWrites;
}