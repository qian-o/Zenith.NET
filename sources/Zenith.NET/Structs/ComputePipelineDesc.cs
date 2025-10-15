namespace Zenith.NET;

public record struct ComputePipelineDesc
{
    public Shader Compute;

    public ResourceLayout[] ResourceLayouts;

    public uint ThreadGroupSizeX;

    public uint ThreadGroupSizeY;

    public uint ThreadGroupSizeZ;
}
