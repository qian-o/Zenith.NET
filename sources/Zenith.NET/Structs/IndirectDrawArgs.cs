namespace Zenith.NET;

public record struct IndirectDrawArgs
{
    public uint VertexCount { get; set; }

    public uint InstanceCount { get; set; }

    public uint FirstVertex { get; set; }

    public uint FirstInstance { get; set; }
}
