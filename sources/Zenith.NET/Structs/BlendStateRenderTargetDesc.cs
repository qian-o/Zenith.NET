namespace Zenith.NET;

public record struct BlendStateRenderTargetDesc
{
    public bool BlendEnable { get; set; }

    public Blend SrcBlend { get; set; }

    public Blend DestBlend { get; set; }

    public BlendOp BlendOp { get; set; }

    public Blend SrcBlendAlpha { get; set; }

    public Blend DestBlendAlpha { get; set; }

    public BlendOp BlendOpAlpha { get; set; }

    public ColorComponentFlags Flags { get; set; }
}
