namespace Zenith.NET;

public record struct IndirectDrawArgs
{
    public uint VertexCount;

    public uint InstanceCount;

    public uint FirstVertex;

    public uint FirstInstance;
}
