namespace Zenith.NET;

public record struct RasterizerState
{
    public CullMode CullMode;

    public FillMode FillMode;

    public FrontFace FrontFace;

    public int DepthBias;

    public float DepthBiasClamp;

    public float SlopeScaledDepthBias;

    public bool DepthClipEnable;

    public bool ScissorEnable;
}
