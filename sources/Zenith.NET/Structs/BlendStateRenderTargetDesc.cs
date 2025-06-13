namespace Zenith.NET;

public record struct BlendStateRenderTargetDesc : IDesc
{
    public bool BlendEnable { get; set; }

    public Blend SrcBlend { get; set; }

    public Blend DestBlend { get; set; }

    public BlendOp BlendOp { get; set; }

    public Blend SrcBlendAlpha { get; set; }

    public Blend DestBlendAlpha { get; set; }

    public BlendOp BlendOpAlpha { get; set; }

    public ColorComponentFlags Flags { get; set; }

    public readonly bool Validate()
    {
        if (!Enum.IsDefined(SrcBlend))
        {
            return false;
        }

        if (!Enum.IsDefined(DestBlend))
        {
            return false;
        }

        if (!Enum.IsDefined(BlendOp))
        {
            return false;
        }

        if (!Enum.IsDefined(SrcBlendAlpha))
        {
            return false;
        }

        if (!Enum.IsDefined(DestBlendAlpha))
        {
            return false;
        }

        if (!Enum.IsDefined(BlendOpAlpha))
        {
            return false;
        }

        return true;
    }
}
