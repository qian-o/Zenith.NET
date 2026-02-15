namespace Zenith.NET;

public record struct GraphicsPipelineDesc
{
    public RenderStates RenderStates;

    public Shader Vertex;

    public Shader Pixel;

    public ResourceLayout? ResourceLayout;

    public InputLayout[] InputLayouts;

    public PrimitiveTopology PrimitiveTopology;

    public Output Output;
}
