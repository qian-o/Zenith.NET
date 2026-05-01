namespace Zenith.NET;

public record struct BlendStateRenderTarget
{
    public bool BlendEnable;

    public BlendFactor SrcFactor;

    public BlendFactor DstFactor;

    public BlendOperation Operation;

    public BlendFactor SrcFactorAlpha;

    public BlendFactor DstFactorAlpha;

    public BlendOperation OperationAlpha;

    public ColorWrites Writes;
}
