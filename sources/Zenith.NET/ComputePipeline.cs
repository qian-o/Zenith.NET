namespace Zenith.NET;

public abstract class ComputePipeline(GraphicsContext context, ComputePipelineDesc desc) : Pipeline(context)
{
    public ComputePipelineDesc Desc { get; } = desc;
}
