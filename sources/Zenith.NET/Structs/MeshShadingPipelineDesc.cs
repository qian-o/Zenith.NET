namespace Zenith.NET;

public record struct MeshShadingPipelineDesc
{
    public RenderState RenderState;

    public Shader? TaskShader;

    public Shader MeshShader;

    public Shader FragmentShader;

    public PrimitiveTopology PrimitiveTopology;

    public AttachmentFormats AttachmentFormats;
}
