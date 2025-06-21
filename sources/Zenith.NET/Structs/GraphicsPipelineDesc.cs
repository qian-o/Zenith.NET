namespace Zenith.NET;

public record struct GraphicsPipelineDesc
{
    public RenderStates RenderStates;

    public GraphicsShaders Shaders;

    public InputLayout[] InputLayouts;

    public ResourceLayout[] ResourceLayouts;

    public PrimitiveTopology PrimitiveTopology;

    public Output Outputs;
}
