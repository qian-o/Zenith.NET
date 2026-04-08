namespace Zenith.NET;

public record struct ComputePipelineDesc
{
    public Shader Compute;

    public ResourceBinding[] ResourceBindings;

    public uint ThreadGroupSizeX;

    public uint ThreadGroupSizeY;

    public uint ThreadGroupSizeZ;
}
