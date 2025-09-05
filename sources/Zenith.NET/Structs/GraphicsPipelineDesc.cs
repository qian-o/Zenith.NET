namespace Zenith.NET;

public record struct GraphicsPipelineDesc
{
    public RenderStates RenderStates;

    public Shader Vertex;

    public Shader? Hull;

    public Shader? Domain;

    public Shader? Geometry;

    public Shader Pixel;

    public ResourceLayout[] ResourceLayouts;

    public InputLayout[] InputLayouts;

    public PrimitiveTopology PrimitiveTopology;

    public Output Outputs;
}
