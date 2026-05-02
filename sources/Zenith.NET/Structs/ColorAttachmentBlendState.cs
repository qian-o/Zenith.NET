namespace Zenith.NET;

public record struct ColorAttachmentBlendState
{
    public bool IsBlendingEnabled;

    public BlendFactor SourceRgbBlendFactor;

    public BlendFactor DestinationRgbBlendFactor;

    public BlendOperation RgbBlendOperation;

    public BlendFactor SourceAlphaBlendFactor;

    public BlendFactor DestinationAlphaBlendFactor;

    public BlendOperation AlphaBlendOperation;

    public ColorWrites ColorWrites;
}