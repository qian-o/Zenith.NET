namespace Zenith.NET;

/// <summary>
/// Describes the arguments required for an indirect indexed draw call.
/// </summary>
public record struct IndirectDrawIndexedArgs
{
    /// <summary>
    /// Initializes a new instance of the <see cref="IndirectDrawIndexedArgs"/> struct with default values.
    /// </summary>
    public IndirectDrawIndexedArgs()
    {
        IndexCount = 0;
        InstanceCount = 0;
        FirstIndex = 0;
        VertexOffset = 0;
        FirstInstance = 0;
    }

    /// <summary>
    /// The number of indices to draw.
    /// </summary>
    public uint IndexCount { get; set; }

    /// <summary>
    /// The number of instances to draw.
    /// </summary>
    public uint InstanceCount { get; set; }

    /// <summary>
    /// The index of the first index.
    /// </summary>
    public uint FirstIndex { get; set; }

    /// <summary>
    /// The value added to each index before reading a vertex from the vertex buffer.
    /// </summary>
    public int VertexOffset { get; set; }

    /// <summary>
    /// The index of the first instance.
    /// </summary>
    public uint FirstInstance { get; set; }
}
