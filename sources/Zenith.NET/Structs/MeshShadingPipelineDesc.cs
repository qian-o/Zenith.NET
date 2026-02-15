namespace Zenith.NET;

public record struct MeshShadingPipelineDesc
{
    public RenderStates RenderStates;

    public Shader? Amplification;

    public Shader Mesh;

    public Shader Pixel;

    public ResourceLayout? ResourceLayout;

    public PrimitiveTopology PrimitiveTopology;

    public Output Output;

    public uint ObjectThreadGroupSizeX;

    public uint ObjectThreadGroupSizeY;

    public uint ObjectThreadGroupSizeZ;

    public uint MeshThreadGroupSizeX;

    public uint MeshThreadGroupSizeY;

    public uint MeshThreadGroupSizeZ;
}
