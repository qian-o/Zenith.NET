namespace Zenith.NET;

public abstract class ComputePipeline(GraphicsContext context, ComputePipelineDesc desc) : GraphicsResource(context), IPipeline
{
    private ComputePipelineDesc desc = desc;

    public ref readonly ComputePipelineDesc Desc => ref desc;
}
