namespace Zenith.NET;

public record struct MeshShadingPipelineDesc
{
    public RenderStates RenderStates;

    public Shader? Amplification;

    public Shader Mesh;

    public Shader Pixel;

    public ResourceSlot[] ResourceSlots;

    public PrimitiveTopology PrimitiveTopology;

    public Output Output;

    public uint AmplificationThreadGroupSizeX;

    public uint AmplificationThreadGroupSizeY;

    public uint AmplificationThreadGroupSizeZ;

    public uint MeshThreadGroupSizeX;

    public uint MeshThreadGroupSizeY;

    public uint MeshThreadGroupSizeZ;
}
