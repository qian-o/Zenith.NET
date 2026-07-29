namespace Zenith.NET;

public struct GraphicsPipelineDesc
{
    public Shader VertexShader;

    public Shader FragmentShader;

    public InputLayout[] InputLayouts;

    public PrimitiveTopology PrimitiveTopology;

    public AttachmentFormats AttachmentFormats;

    public RenderState RenderState;
}
