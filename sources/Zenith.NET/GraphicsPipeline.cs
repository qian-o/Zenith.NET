namespace Zenith.NET;

public abstract class GraphicsPipeline(GraphicsContext context, GraphicsPipelineDesc desc) : GraphicsResource(context), IPipeline
{
    private GraphicsPipelineDesc desc = desc;

    public ref readonly GraphicsPipelineDesc Desc => ref desc;
}
