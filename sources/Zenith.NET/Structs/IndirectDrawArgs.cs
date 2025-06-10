namespace Zenith.NET;

public record struct IndirectDrawArgs
{
    public IndirectDrawArgs()
    {
        VertexCount = 0;
        InstanceCount = 0;
        FirstVertex = 0;
        FirstInstance = 0;
    }

    public uint VertexCount { get; set; }

    public uint InstanceCount { get; set; }

    public uint FirstVertex { get; set; }

    public uint FirstInstance { get; set; }
}
