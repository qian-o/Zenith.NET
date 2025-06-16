namespace Zenith.NET;

public record struct RasterizerStateDesc
{
    public CullMode CullMode { get; set; }

    public FillMode FillMode { get; set; }

    public FrontFace FrontFace { get; set; }

    public int DepthBias { get; set; }

    public float DepthBiasClamp { get; set; }

    public float SlopeScaledDepthBias { get; set; }

    public bool DepthClipEnable { get; set; }

    public bool ScissorEnable { get; set; }
}
