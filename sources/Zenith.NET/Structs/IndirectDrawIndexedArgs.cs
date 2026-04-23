namespace Zenith.NET;

public record struct IndirectDrawIndexedArgs
{
    public uint IndexCount;

    public uint InstanceCount;

    public uint FirstIndex;

    public int VertexOffset;

    public uint FirstInstance;
}
