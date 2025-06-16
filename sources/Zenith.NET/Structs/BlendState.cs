namespace Zenith.NET;

public struct BlendState
{
    public bool AlphaToCoverageEnable { get; set; }

    public bool IndependentBlendEnable { get; set; }

    public BlendStateRenderTarget RenderTarget0 { get; set; }

    public BlendStateRenderTarget RenderTarget1 { get; set; }

    public BlendStateRenderTarget RenderTarget2 { get; set; }

    public BlendStateRenderTarget RenderTarget3 { get; set; }

    public BlendStateRenderTarget RenderTarget4 { get; set; }

    public BlendStateRenderTarget RenderTarget5 { get; set; }

    public BlendStateRenderTarget RenderTarget6 { get; set; }

    public BlendStateRenderTarget RenderTarget7 { get; set; }
}