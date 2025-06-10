namespace Zenith.NET;

public abstract class GraphicsPipeline(GraphicsContext context, GraphicsPipelineDesc desc) : Pipeline(context)
{
    public GraphicsPipelineDesc Desc { get; } = desc;
}
