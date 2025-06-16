using System.Numerics;

namespace Zenith.NET;

public struct RenderStates
{
    public RasterizerState RasterizerState { get; set; }

    public DepthStencilState DepthStencilState { get; set; }

    public BlendState BlendState { get; set; }

    public int StencilReference { get; set; }

    public Vector4? BlendFactor { get; set; }
}
