namespace Zenith.NET;

public record struct BlendStateDesc : IDesc
{
    public BlendStateDesc()
    {
        AlphaToCoverageEnable = false;
        IndependentBlendEnable = false;
        RenderTarget0 = new();
        RenderTarget1 = new();
        RenderTarget2 = new();
        RenderTarget3 = new();
        RenderTarget4 = new();
        RenderTarget5 = new();
        RenderTarget6 = new();
        RenderTarget7 = new();
    }

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

    public readonly bool Validate()
    {
        return RenderTarget0.Validate()
               && RenderTarget1.Validate()
               && RenderTarget2.Validate()
               && RenderTarget3.Validate()
               && RenderTarget4.Validate()
               && RenderTarget5.Validate()
               && RenderTarget6.Validate()
               && RenderTarget7.Validate();
    }
}