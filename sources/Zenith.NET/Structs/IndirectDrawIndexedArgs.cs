namespace Zenith.NET;

public struct IndirectDrawIndexedArgs
{
    public uint IndexCount { get; set; }

    public uint InstanceCount { get; set; }

    public uint FirstIndex { get; set; }

    public int VertexOffset { get; set; }

    public uint FirstInstance { get; set; }
}
