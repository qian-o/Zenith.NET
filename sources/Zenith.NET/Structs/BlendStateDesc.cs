namespace Zenith.NET;

public record struct BlendStateDesc
{
    public bool AlphaToCoverageEnable { get; set; }

    public bool IndependentBlendEnable { get; set; }

    public BlendStateRenderTargetDesc RenderTarget0 { get; set; }

    public BlendStateRenderTargetDesc RenderTarget1 { get; set; }

    public BlendStateRenderTargetDesc RenderTarget2 { get; set; }

    public BlendStateRenderTargetDesc RenderTarget3 { get; set; }

    public BlendStateRenderTargetDesc RenderTarget4 { get; set; }

    public BlendStateRenderTargetDesc RenderTarget5 { get; set; }

    public BlendStateRenderTargetDesc RenderTarget6 { get; set; }

    public BlendStateRenderTargetDesc RenderTarget7 { get; set; }
}