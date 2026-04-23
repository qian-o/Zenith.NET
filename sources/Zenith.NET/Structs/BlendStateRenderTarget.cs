namespace Zenith.NET;

public record struct BlendStateRenderTarget
{
    public bool BlendEnable;

    public Blend SrcBlend;

    public Blend DestBlend;

    public BlendOp BlendOp;

    public Blend SrcBlendAlpha;

    public Blend DestBlendAlpha;

    public BlendOp BlendOpAlpha;

    public ColorComponentFlags Flags;
}
