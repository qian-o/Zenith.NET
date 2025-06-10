namespace Zenith.NET;

public record struct IndirectDrawIndexedArgs
{
    public IndirectDrawIndexedArgs()
    {
        IndexCount = 0;
        InstanceCount = 0;
        FirstIndex = 0;
        VertexOffset = 0;
        FirstInstance = 0;
    }

    public uint IndexCount { get; set; }

    public uint InstanceCount { get; set; }

    public uint FirstIndex { get; set; }

    public int VertexOffset { get; set; }

    public uint FirstInstance { get; set; }
}
