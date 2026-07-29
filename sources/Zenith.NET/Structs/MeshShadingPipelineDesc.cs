namespace Zenith.NET;

public struct MeshShadingPipelineDesc
{
    public Shader? TaskShader;

    public Shader MeshShader;

    public Shader FragmentShader;

    public PrimitiveTopology PrimitiveTopology;

    public AttachmentFormats AttachmentFormats;

    public RenderState RenderState;
}
