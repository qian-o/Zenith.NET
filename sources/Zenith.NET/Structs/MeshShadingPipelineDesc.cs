namespace Zenith.NET;

public record struct MeshShadingPipelineDesc
{
    public RenderState RenderState;

    public Shader MeshShader;

    public Shader FragmentShader;

    public ResourceLayout[] ResourceLayouts;

    public PrimitiveTopology PrimitiveTopology;

    public AttachmentFormats AttachmentFormats;

    public Shader? TaskShader;
}
