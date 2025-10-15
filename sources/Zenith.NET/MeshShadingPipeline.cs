namespace Zenith.NET;

public abstract class MeshShadingPipeline(GraphicsContext context, MeshShadingPipelineDesc desc) : Pipeline(context)
{
    private MeshShadingPipelineDesc desc = desc;

    public ref readonly MeshShadingPipelineDesc Desc => ref desc;
}
