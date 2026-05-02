namespace Zenith.NET;

public record struct MeshShadingPipelineDesc
{
    public RenderState RenderState;

    public Shader? TaskShader;

    public Shader MeshShader;

    public Shader FragmentShader;

    public ResourceLayout[] ResourceLayouts;

    public PrimitiveTopology PrimitiveTopology;

    public AttachmentFormats AttachmentFormats;
}
