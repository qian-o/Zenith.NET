namespace Zenith.NET;

public abstract class RayTracingPipeline(GraphicsContext context, RayTracingPipelineDesc desc) : Pipeline(context)
{
    private RayTracingPipelineDesc desc = desc;

    public ref readonly RayTracingPipelineDesc Desc => ref desc;
}
