namespace Zenith.NET;

public record struct MeshShadingPipelineDesc
{
    public RenderStates RenderStates;

    public Shader? Task;

    public Shader Mesh;

    public Shader Fragment;

    public ResourceBinding[] Bindings;

    public PrimitiveTopology PrimitiveTopology;

    public AttachmentFormats Formats;
}
