namespace Zenith.NET;

public record struct RasterizerState
{
    public FillMode FillMode;

    public CullMode CullMode;

    public FrontFace FrontFace;

    public int DepthBias;

    public float DepthBiasClamp;

    public float DepthBiasSlopeScale;

    public bool IsDepthClipEnabled;

    public bool IsScissorEnabled;
}
