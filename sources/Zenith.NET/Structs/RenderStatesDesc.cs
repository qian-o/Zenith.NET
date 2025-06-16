using System.Numerics;

namespace Zenith.NET;

public record struct RenderStatesDesc
{
    public RasterizerStateDesc RasterizerState { get; set; }

    public DepthStencilStateDesc DepthStencilState { get; set; }

    public BlendStateDesc BlendState { get; set; }

    public int StencilReference { get; set; }

    public Vector4? BlendFactor { get; set; }
}
