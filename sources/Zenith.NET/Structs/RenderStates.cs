using System.Numerics;

namespace Zenith.NET;

public record struct RenderStates
{
    public RasterizerState RasterizerState;

    public DepthStencilState DepthStencilState;

    public BlendState BlendState;

    public uint StencilReference;

    public Vector4? BlendConstant;
}
