using Metal.NET;

namespace Zenith.NET.Metal;

internal class MTLGraphicsPipeline : GraphicsPipeline
{
    public MTLRenderPipelineState RenderPipelineState;

    public MTLDepthStencilState DepthStencilState;

    public MTLGraphicsPipeline(MTLGraphicsContext context, GraphicsPipelineDesc desc) : base(context, desc)
    {
        throw new NotImplementedException();
    }

    protected override void SetResourceName(string name)
    {
    }

    protected override void Destroy()
    {
        DepthStencilState.Dispose();

        RenderPipelineState.Dispose();
    }
}
