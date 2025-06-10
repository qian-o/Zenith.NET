namespace Zenith.NET;

public abstract class RayTracingPipeline(GraphicsContext context, RayTracingPipelineDesc desc) : Pipeline(context)
{
    public RayTracingPipelineDesc Desc { get; } = desc;
}
