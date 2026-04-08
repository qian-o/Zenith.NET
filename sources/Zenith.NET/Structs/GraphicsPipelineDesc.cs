namespace Zenith.NET;

public record struct GraphicsPipelineDesc
{
    public RenderStates RenderStates;

    public Shader Vertex;

    public Shader Pixel;

    public ResourceBinding[] ResourceBindings;

    public InputLayout[] InputLayouts;

    public PrimitiveTopology PrimitiveTopology;

    public Output Output;
}
