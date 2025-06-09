using System.Numerics;

namespace Zenith.NET;

/// <summary>
/// Describes a set of triangles for use in acceleration structures, including vertex and index data, format, and transform.
/// </summary>
public record struct RayTracingTrianglesDesc : IRayTracingGeometryDesc
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RayTracingTrianglesDesc"/> struct with default values.
    /// </summary>
    public RayTracingTrianglesDesc()
    {
        VertexBuffer = null!;
        VertexFormat = PixelFormat.R32G32B32A32Float;
        VertexCount = 0;
        VertexStrideInBytes = 1;
        VertexOffsetInBytes = 0;
        IndexBuffer = null;
        IndexFormat = IndexFormat.UInt32;
        IndexCount = 0;
        IndexOffsetInBytes = 0;
        Transform = Matrix4x4.Identity;
        Flags = RayTracingGeometryFlags.None;
    }

    /// <summary>
    /// The buffer containing vertex data.
    /// </summary>
    public Buffer VertexBuffer { get; set; }

    /// <summary>
    /// The format of the vertex data.
    /// </summary>
    public PixelFormat VertexFormat { get; set; }

    /// <summary>
    /// The number of vertices.
    /// </summary>
    public uint VertexCount { get; set; }

    /// <summary>
    /// The stride, in bytes, between consecutive vertices.
    /// </summary>
    public uint VertexStrideInBytes { get; set; }

    /// <summary>
    /// The offset, in bytes, from the start of the vertex buffer to the first vertex.
    /// </summary>
    public uint VertexOffsetInBytes { get; set; }

    /// <summary>
    /// The buffer containing index data, or <c>null</c> for non-indexed geometry.
    /// </summary>
    public Buffer? IndexBuffer { get; set; }

    /// <summary>
    /// The format of the index data.
    /// </summary>
    public IndexFormat IndexFormat { get; set; }

    /// <summary>
    /// The number of indices.
    /// </summary>
    public uint IndexCount { get; set; }

    /// <summary>
    /// The offset, in bytes, from the start of the index buffer to the first index.
    /// </summary>
    public uint IndexOffsetInBytes { get; set; }

    /// <summary>
    /// The 4x4 transformation matrix applied to the geometry.
    /// Only the upper 3x4 portion (rotation, scale, translation) is used; the last row is ignored.
    /// </summary>
    public Matrix4x4 Transform { get; set; }

    /// <summary>
    /// Gets or sets the geometry flags that specify options for the triangles in acceleration structures.
    /// </summary>
    public RayTracingGeometryFlags Flags { get; set; }

    /// <summary>
    /// Validates the current <see cref="RayTracingTrianglesDesc"/> instance.
    /// </summary>
    /// <returns><c>true</c> if valid; otherwise, <c>false</c>.</returns>
    public readonly bool Validate()
    {
        if (VertexBuffer is null)
        {
            return false;
        }

        if (!Enum.IsDefined(VertexFormat))
        {
            return false;
        }

        if (VertexCount is 0)
        {
            return false;
        }

        if (VertexStrideInBytes is 0)
        {
            return false;
        }

        if (IndexBuffer is not null)
        {
            if (!Enum.IsDefined(IndexFormat))
            {
                return false;
            }

            if (IndexCount is 0)
            {
                return false;
            }
        }

        return true;
    }
}
