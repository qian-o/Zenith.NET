namespace Zenith.NET;

public record struct MeshShadingPipelineDesc
{
    public RenderStates RenderStates;

    public Shader? Amplification;

    public Shader Mesh;

    public Shader Pixel;

    public ResourceLayout[] ResourceLayouts;

    public PrimitiveTopology PrimitiveTopology;

    public Output Output;
}
