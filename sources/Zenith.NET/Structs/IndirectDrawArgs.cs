namespace Zenith.NET;

/// <summary>
/// Describes the arguments required for an indirect non-indexed draw call.
/// </summary>
public record struct IndirectDrawArgs
{
    /// <summary>
    /// Initializes a new instance of the <see cref="IndirectDrawArgs"/> struct with default values.
    /// </summary>
    public IndirectDrawArgs()
    {
        VertexCount = 0;
        InstanceCount = 0;
        FirstVertex = 0;
        FirstInstance = 0;
    }

    /// <summary>
    /// The number of vertices to draw.
    /// </summary>
    public uint VertexCount { get; set; }

    /// <summary>
    /// The number of instances to draw.
    /// </summary>
    public uint InstanceCount { get; set; }

    /// <summary>
    /// The index of the first vertex.
    /// </summary>
    public uint FirstVertex { get; set; }

    /// <summary>
    /// The index of the first instance.
    /// </summary>
    public uint FirstInstance { get; set; }
}
