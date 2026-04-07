namespace Zenith.NET;

public record struct ComputePipelineDesc
{
    public Shader Compute;

    public ResourceSlot[] ResourceSlots;

    public uint ThreadGroupSizeX;

    public uint ThreadGroupSizeY;

    public uint ThreadGroupSizeZ;
}
