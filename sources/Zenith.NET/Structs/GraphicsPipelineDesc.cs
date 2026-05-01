namespace Zenith.NET;

public record struct GraphicsPipelineDesc
{
    public RenderStates RenderStates;

    public Shader Vertex;

    public Shader Fragment;

    public ResourceBinding[] Bindings;

    public InputLayout[] Layouts;

    public PrimitiveTopology PrimitiveTopology;

    public AttachmentFormats Formats;
}
