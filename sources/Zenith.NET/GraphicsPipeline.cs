namespace Zenith.NET;

public abstract class GraphicsPipeline(GraphicsContext context, GraphicsPipelineDesc desc) : Pipeline(context)
{
    private GraphicsPipelineDesc desc = desc;

    public ref readonly GraphicsPipelineDesc Desc => ref desc;
}
