namespace Zenith.NET;

public record struct GraphicsPipelineDesc
{
    public RenderStates RenderStates;

    public Shader Vertex;

    public Shader Pixel;

    public ResourceSlot[] ResourceSlots;

    public InputLayout[] InputLayouts;

    public PrimitiveTopology PrimitiveTopology;

    public Output Output;
}
