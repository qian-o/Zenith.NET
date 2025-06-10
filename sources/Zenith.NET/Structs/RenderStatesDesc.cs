using System.Numerics;

namespace Zenith.NET;

public record struct RenderStatesDesc : IDesc
{
    public RenderStatesDesc()
    {
        RasterizerState = new();
        DepthStencilState = new();
        BlendState = new();
        StencilReference = 0;
        BlendFactor = null;
    }

    public RasterizerStateDesc RasterizerState { get; set; }

    public DepthStencilStateDesc DepthStencilState { get; set; }

    public BlendStateDesc BlendState { get; set; }

    public int StencilReference { get; set; }

    public Vector4? BlendFactor { get; set; }

    public readonly bool Validate()
    {
        if (!RasterizerState.Validate())
        {
            return false;
        }

        if (!DepthStencilState.Validate())
        {
            return false;
        }

        if (!BlendState.Validate())
        {
            return false;
        }

        return true;
    }
}
