using System.Numerics;

namespace Zenith.NET;

public record struct RenderStates
{
    public RasterizerState RasterizerState;

    public DepthStencilState DepthStencilState;

    public BlendState BlendState;

    public int StencilReference;

    public Vector4? BlendFactor;
}
