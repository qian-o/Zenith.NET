namespace Zenith.NET;

public record struct GraphicsPipelineDesc
{
    public RenderState RenderState;

    public Shader VertexShader;

    public Shader FragmentShader;

    public ResourceLayout[] ResourceLayouts;

    public InputLayout[] InputLayouts;

    public PrimitiveTopology PrimitiveTopology;

    public AttachmentFormats AttachmentFormats;
}
