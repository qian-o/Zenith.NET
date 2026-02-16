namespace Zenith.NET;

public record struct ComputePipelineDesc
{
    public Shader Compute;

    public ResourceLayout? ResourceLayout;

    public uint ThreadGroupSizeX;

    public uint ThreadGroupSizeY;

    public uint ThreadGroupSizeZ;
}
