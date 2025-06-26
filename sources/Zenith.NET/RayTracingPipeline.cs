namespace Zenith.NET;

public abstract class RayTracingPipeline(GraphicsContext context, RayTracingPipelineDesc desc) : GraphicsResource(context), IPipeline
{
    private RayTracingPipelineDesc desc = desc;

    public ref readonly RayTracingPipelineDesc Desc => ref desc;
}
