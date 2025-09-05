namespace Zenith.NET;

public record struct MeshShadingPipelineDesc
{
    public Shader? Amplification;

    public Shader Mesh;

    public Shader Pixel;

    public ResourceLayout[] ResourceLayouts;

    public RenderStates RenderStates;

    public PrimitiveTopology PrimitiveTopology;

    public Output Outputs;
}
