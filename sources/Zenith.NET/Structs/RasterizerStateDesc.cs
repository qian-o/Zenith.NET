namespace Zenith.NET;

public record struct RasterizerStateDesc : IDesc
{
    public CullMode CullMode { get; set; }

    public FillMode FillMode { get; set; }

    public FrontFace FrontFace { get; set; }

    public int DepthBias { get; set; }

    public float DepthBiasClamp { get; set; }

    public float SlopeScaledDepthBias { get; set; }

    public bool DepthClipEnable { get; set; }

    public bool ScissorEnable { get; set; }

    public readonly bool Validate()
    {
        if (!Enum.IsDefined(CullMode))
        {
            return false;
        }

        if (!Enum.IsDefined(FillMode))
        {
            return false;
        }

        if (!Enum.IsDefined(FrontFace))
        {
            return false;
        }

        return true;
    }
}
